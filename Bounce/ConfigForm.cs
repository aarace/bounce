using System.Drawing;
using System.Windows.Forms;

namespace Bounce
{
    /// <summary>
    /// The "/c" configuration dialog: ball size/count, speed, color, an
    /// optional image file to use instead of a plain circle (the hook for
    /// swapping in a company logo later without touching any code), a
    /// corner-hit flash, and an optional fading trail behind the ball(s).
    /// </summary>
    public sealed class ConfigForm : Form
    {
        private readonly NumericUpDown _sizeInput = new NumericUpDown { Minimum = 8, Maximum = 400, Value = 48 };
        private readonly NumericUpDown _ballCountInput = new NumericUpDown { Minimum = Settings.MinBallCount, Maximum = Settings.MaxBallCount, Value = 1 };

        private readonly TrackBar _speedSlider = new TrackBar
        {
            Minimum = Settings.MinSpeed,
            Maximum = Settings.MaxSpeed,
            TickStyle = TickStyle.BottomRight,
            TickFrequency = 5,
            Width = 220
        };
        private readonly Label _speedValueLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };

        private readonly Button _colorButton = new Button { Text = "Choose color...", AutoSize = true };
        private readonly TextBox _imagePathBox = new TextBox { ReadOnly = true, Width = 220 };
        private readonly Button _browseButton = new Button { Text = "Browse...", AutoSize = true };
        private readonly Button _clearImageButton = new Button { Text = "Clear", AutoSize = true };

        private readonly CheckBox _showTrailCheckBox = new CheckBox { Text = "Show trail behind the ball", AutoSize = true };

        // One tick past MaxTrailAgeSeconds represents "Forever" - see UpdateTrailAgeLabel.
        private readonly TrackBar _trailAgeSlider = new TrackBar
        {
            Minimum = Settings.MinTrailAgeSeconds,
            Maximum = Settings.MaxTrailAgeSeconds + 1,
            TickStyle = TickStyle.BottomRight,
            TickFrequency = 5,
            Width = 220
        };
        private readonly Label _trailAgeValueLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };

        private readonly CheckBox _cornerFlashCheckBox = new CheckBox { Text = "Flash on corner hit", AutoSize = true };

        private Color _selectedColor;

        public ConfigForm()
        {
            Text = "Bounce Screensaver Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var settings = Settings.Load();
            _sizeInput.Value = Clamp(settings.BallSize, (int)_sizeInput.Minimum, (int)_sizeInput.Maximum);
            _ballCountInput.Value = Clamp(settings.BallCount, Settings.MinBallCount, Settings.MaxBallCount);
            _speedSlider.Value = Clamp(settings.Speed, _speedSlider.Minimum, _speedSlider.Maximum);
            _selectedColor = settings.BallColor;
            _imagePathBox.Text = settings.ImagePath;
            _showTrailCheckBox.Checked = settings.ShowTrail;
            _trailAgeSlider.Value = settings.TrailMaxAgeSeconds == Settings.ForeverTrailAge
                ? _trailAgeSlider.Maximum
                : Clamp(settings.TrailMaxAgeSeconds, _trailAgeSlider.Minimum, _trailAgeSlider.Maximum - 1);
            _cornerFlashCheckBox.Checked = settings.CornerFlash;

            UpdateColorButton();
            UpdateSpeedLabel();
            UpdateTrailAgeLabel();
            UpdateTrailAgeEnabled();

            _speedSlider.ValueChanged += (s, e) => UpdateSpeedLabel();
            _trailAgeSlider.ValueChanged += (s, e) => UpdateTrailAgeLabel();
            _showTrailCheckBox.CheckedChanged += (s, e) => UpdateTrailAgeEnabled();

            _colorButton.Click += (s, e) =>
            {
                using (var dialog = new ColorDialog { Color = _selectedColor })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _selectedColor = dialog.Color;
                        UpdateColorButton();
                    }
                }
            };

            _browseButton.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog { Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*" })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _imagePathBox.Text = dialog.FileName;
                    }
                }
            };

            _clearImageButton.Click += (s, e) => _imagePathBox.Text = string.Empty;

            var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            okButton.Click += (s, e) => SaveAndClose();
            // Setting DialogResult alone only auto-closes a form shown via
            // ShowDialog(); this form runs via Application.Run (see
            // Program.cs), so Cancel needs an explicit Close() too.
            cancelButton.Click += (s, e) => Close();

            var fields = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(4)
            };

            fields.Controls.Add(new Label { Text = "Ball/trail size (px):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            fields.Controls.Add(_sizeInput, 1, 0);

            fields.Controls.Add(new Label { Text = "Number of balls:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            fields.Controls.Add(_ballCountInput, 1, 1);

            fields.Controls.Add(new Label { Text = "Speed:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            fields.Controls.Add(_speedSlider, 1, 2);
            fields.Controls.Add(_speedValueLabel, 2, 2);

            fields.Controls.Add(new Label { Text = "Color:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            fields.Controls.Add(_colorButton, 1, 3);

            fields.Controls.Add(new Label { Text = "Custom image\n(e.g. a logo):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
            fields.Controls.Add(_imagePathBox, 1, 4);

            var imageButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            imageButtons.Controls.Add(_browseButton);
            imageButtons.Controls.Add(_clearImageButton);
            fields.Controls.Add(imageButtons, 2, 4);

            fields.Controls.Add(_cornerFlashCheckBox, 1, 5);

            fields.Controls.Add(_showTrailCheckBox, 1, 6);

            fields.Controls.Add(new Label { Text = "Trail length:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
            fields.Controls.Add(_trailAgeSlider, 1, 7);
            fields.Controls.Add(_trailAgeValueLabel, 2, 7);

            var buttonRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom };
            buttonRow.Controls.Add(cancelButton);
            buttonRow.Controls.Add(okButton);

            var root = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, RowCount = 2 };
            root.Controls.Add(fields, 0, 0);
            root.Controls.Add(buttonRow, 0, 1);

            Controls.Add(root);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void UpdateColorButton()
        {
            _colorButton.BackColor = _selectedColor;
            _colorButton.ForeColor = GetReadableForeColor(_selectedColor);
        }

        private void UpdateSpeedLabel()
        {
            _speedValueLabel.Text = _speedSlider.Value.ToString();
        }

        private void UpdateTrailAgeLabel()
        {
            _trailAgeValueLabel.Text = IsTrailForever() ? "Forever" : _trailAgeSlider.Value + "s";
        }

        private void UpdateTrailAgeEnabled()
        {
            _trailAgeSlider.Enabled = _showTrailCheckBox.Checked;
            _trailAgeValueLabel.Enabled = _showTrailCheckBox.Checked;
        }

        private bool IsTrailForever()
        {
            return _trailAgeSlider.Value >= _trailAgeSlider.Maximum;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static Color GetReadableForeColor(Color background)
        {
            double luminance = 0.299 * background.R + 0.587 * background.G + 0.114 * background.B;
            return luminance > 150 ? Color.Black : Color.White;
        }

        private void SaveAndClose()
        {
            var settings = new Settings
            {
                BallSize = (int)_sizeInput.Value,
                Speed = _speedSlider.Value,
                BallColor = _selectedColor,
                ImagePath = _imagePathBox.Text ?? string.Empty,
                ShowTrail = _showTrailCheckBox.Checked,
                TrailMaxAgeSeconds = IsTrailForever() ? Settings.ForeverTrailAge : _trailAgeSlider.Value,
                CornerFlash = _cornerFlashCheckBox.Checked,
                BallCount = (int)_ballCountInput.Value
            };
            settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
