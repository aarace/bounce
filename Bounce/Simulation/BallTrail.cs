using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace Bounce.Simulation
{
    internal readonly struct TrailPoint
    {
        public readonly int BallId;
        public readonly PointF Position;
        public readonly double RecordedAtSeconds;

        public TrailPoint(int ballId, PointF position, double recordedAtSeconds)
        {
            BallId = ballId;
            Position = position;
            RecordedAtSeconds = recordedAtSeconds;
        }
    }

    /// <summary>
    /// Records recent ball positions (from one or more balls, each
    /// identified by an arbitrary caller-chosen "ball id") to render as a
    /// fading "shadow" trail behind them. Also doubles as a diagnostic:
    /// crank the speed up, set the trail to never fade, and let it run to
    /// visually confirm the ball(s) have reached every part of the
    /// multi-monitor layout.
    /// </summary>
    internal sealed class BallTrail
    {
        // Independent of the age setting: a hard cap on stored points so a
        // long "never fade" run can't grow memory/render cost without bound.
        // Shared across every ball, so more balls means less history each.
        private const int MaxStoredPoints = 20000;

        // Skip recording a new point for a given ball unless it has moved
        // at least this many pixels since its own last recorded point, so
        // the trail's density (and therefore render cost) tracks distance
        // traveled, not frame rate.
        private const float MinDistanceBetweenPoints = 3f;

        // Forever-mode segments are drawn at this constant alpha rather
        // than fully opaque, so overlapping passes build up a "heat map"
        // of well-traveled paths without any single point standing out.
        private const float ForeverAlpha = 0.35f;

        // Queue rather than LinkedList: we only ever add at the end and
        // remove from the front, and Queue<T> is backed by a contiguous
        // array, so both that and iterating it (once per tick, below) are
        // much cheaper than a doubly-linked list once this holds thousands
        // of points.
        private readonly Queue<TrailPoint> _points = new Queue<TrailPoint>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        // Recomputed once per tick (from Record) rather than once per
        // GetSegments() call - every MonitorWindow asks for segments on
        // every frame, so without this a multi-monitor setup would redo
        // this work several times per tick for no reason.
        private readonly List<(PointF From, PointF To, float Alpha)> _segmentCache =
            new List<(PointF From, PointF To, float Alpha)>();

        // Segments newly added this tick in "forever" mode, drained by the
        // caller (see GetAndClearPendingForeverSegments) and painted once
        // each onto a persistent surface rather than ever being redrawn.
        private readonly List<(PointF From, PointF To, float Alpha)> _pendingForeverSegments =
            new List<(PointF From, PointF To, float Alpha)>();

        // Each ball's own last recorded position, keyed by its caller-given
        // id - needed so that with multiple balls sharing this one trail,
        // a segment only ever connects a single ball's own consecutive
        // points, never one ball's point to a different ball's.
        private readonly Dictionary<int, PointF> _lastRecordedByBall = new Dictionary<int, PointF>();

        /// <summary>Null means "never fade" (<see cref="Settings.ForeverTrailAge"/>).</summary>
        public double? MaxAgeSeconds { get; set; }

        /// <summary>Records a new point for the given ball, unless it hasn't moved far enough since its own last one.</summary>
        public void Record(int ballId, PointF position)
        {
            PointF? previous = _lastRecordedByBall.TryGetValue(ballId, out var last) ? (PointF?)last : null;

            if (previous.HasValue)
            {
                float dx = position.X - previous.Value.X;
                float dy = position.Y - previous.Value.Y;
                if ((dx * dx) + (dy * dy) < MinDistanceBetweenPoints * MinDistanceBetweenPoints)
                {
                    return;
                }
            }

            _lastRecordedByBall[ballId] = position;

            if (MaxAgeSeconds.HasValue)
            {
                // _points only ever feeds RebuildSegmentCache, which only
                // runs in this (finite fade) mode - forever mode below
                // renders from _pendingForeverSegments instead, so there's
                // no point maintaining this queue when it is null.
                double now = _clock.Elapsed.TotalSeconds;
                _points.Enqueue(new TrailPoint(ballId, position, now));

                while (_points.Count > MaxStoredPoints)
                {
                    _points.Dequeue();
                }

                double cutoff = now - MaxAgeSeconds.Value;
                while (_points.Count > 0 && _points.Peek().RecordedAtSeconds < cutoff)
                {
                    _points.Dequeue();
                }

                // Finite fade mode: every point's alpha keeps changing as it
                // ages, so the only correct approach is redrawing the whole
                // (naturally bounded, by MaxAgeSeconds) trail each frame.
                // Deferred to RefreshSegments(), called once per tick after
                // every ball's Record() - rebuilding here instead would
                // redo the same whole-cache work once per ball per tick.
            }
            else if (previous.HasValue)
            {
                // Forever mode: alpha is constant, so only the newest
                // segment is new information.
                _pendingForeverSegments.Add((previous.Value, position, ForeverAlpha));
            }
        }

        /// <summary>
        /// Recomputes the finite (fading) mode segment cache with each
        /// point's current age-based alpha. Callers should call this once
        /// per tick, after every ball sharing this trail has had a chance
        /// to <see cref="Record"/> a point for that tick - not once per
        /// ball, which would redo this same whole-cache rebuild several
        /// times for no reason. No-op in "forever" mode, which doesn't use
        /// the segment cache at all.
        /// </summary>
        public void RefreshSegments()
        {
            if (MaxAgeSeconds.HasValue)
            {
                RebuildSegmentCache(_clock.Elapsed.TotalSeconds);
            }
        }

        private void RebuildSegmentCache(double now)
        {
            _segmentCache.Clear();

            // Points from different balls can be interleaved in _points
            // (they're enqueued in whatever order Record was called this
            // tick), so pairing must track each ball's own last-seen point
            // rather than assuming consecutive queue entries belong to the
            // same ball.
            var previousByBall = new Dictionary<int, TrailPoint>();

            foreach (var point in _points)
            {
                if (previousByBall.TryGetValue(point.BallId, out var previous))
                {
                    float alpha = (float)Math.Max(0, 1 - ((now - point.RecordedAtSeconds) / MaxAgeSeconds.Value));
                    if (alpha > 0)
                    {
                        _segmentCache.Add((previous.Position, point.Position, alpha));
                    }
                }

                previousByBall[point.BallId] = point;
            }
        }

        /// <summary>
        /// Consecutive point pairs to draw as segments, each with an alpha
        /// in (0,1] fading toward 0 as a point approaches
        /// <see cref="MaxAgeSeconds"/>. Only meaningful in finite (fading)
        /// mode - empty in "forever" mode, whose segments are consumed via
        /// <see cref="GetAndClearPendingForeverSegments"/> instead.
        /// </summary>
        public IReadOnlyList<(PointF From, PointF To, float Alpha)> GetSegments()
        {
            return _segmentCache;
        }

        /// <summary>
        /// In "forever" mode, the segments newly recorded since the last
        /// call, for the caller to paint once each onto a persistent
        /// surface. Always empty in finite (fading) mode.
        /// </summary>
        public IReadOnlyList<(PointF From, PointF To, float Alpha)> GetAndClearPendingForeverSegments()
        {
            if (_pendingForeverSegments.Count == 0)
            {
                return Array.Empty<(PointF From, PointF To, float Alpha)>();
            }

            var pending = new List<(PointF From, PointF To, float Alpha)>(_pendingForeverSegments);
            _pendingForeverSegments.Clear();
            return pending;
        }
    }
}
