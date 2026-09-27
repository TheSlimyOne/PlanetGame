using System;
using Godot;

namespace PlanetGame.Util.DebugUIComponents
{
    public partial class TextureComponent : PanelContainer, IDebugComponent
    {
        public string TechnicalName { get; set; }
        public bool IsTemplate { get; set; }

        private Label _label;
        private Button _button;
        private TextureRect _textureRect;
        private Control _spacer;

        private Func<Rid> _getRid;
        private Texture2Drd _texture;
        private Rid _currentRid;

        public void GetNodes()
        {
            _label ??= GetNode<Label>("%Label");
            _button ??= GetNode<Button>("%Button");
            _textureRect ??= GetNode<TextureRect>("%TextureRect");
            _spacer ??= FindChild("Spacer") as Control;
        }

        public void Initialize(string name, TextureRect textureRect, bool isVisible = true, bool isTemplate = false)
        {
            GetNodes();

            TechnicalName = name.ToCamelCase();
            IsTemplate = isTemplate;

            Name = $"{TechnicalName}TextureComponent";
            _label.Text = name;

            SetupMouseInput();

            _textureRect.ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional;

            if (textureRect == null)
            {
                _textureRect.Texture = new PlaceholderTexture2D();
                return;
            }

            _textureRect.Texture = textureRect.Texture;
            _textureRect.Material = textureRect.Material;
            _textureRect.TextureFilter = textureRect.TextureFilter;
            _textureRect.StretchMode = textureRect.StretchMode;
            _textureRect.Visible = isVisible;

            UpdateButtonText();
        }

        public void Initialize(string name, Func<Rid> getRid, bool isVisible = true)
        {
            GetNodes();

            TechnicalName = name.ToCamelCase();
            Name = $"{TechnicalName}TextureComponent";
            _label.Text = name;

            SetupMouseInput();

            _getRid = getRid;
            _texture = new Texture2Drd();

            _textureRect.Texture = _texture;
            _textureRect.ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional;
            _textureRect.Visible = isVisible;

            UpdateButtonText();
            UpdateRid();
        }

        private void SetupMouseInput()
        {
            MouseFilter = MouseFilterEnum.Stop;
            MouseDefaultCursorShape = CursorShape.PointingHand;

            _label.MouseFilter = MouseFilterEnum.Ignore;
            _button.MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (IsTemplate)
                return;

            if (@event is not InputEventMouseButton mouseEvent)
                return;

            if (mouseEvent.ButtonIndex != MouseButton.Left || !mouseEvent.Pressed)
                return;

            ToggleTexture();
            AcceptEvent();
        }

        public override void _Process(double delta)
        {
            if (_getRid == null)
                return;

            UpdateRid();
        }

        private void UpdateRid()
        {
            Rid rid = _getRid();

            if (!rid.IsValid || rid == _currentRid)
                return;

            _currentRid = rid;
            _texture.TextureRdRid = new();
            _texture.TextureRdRid = rid;
        }

        private void ToggleTexture()
        {
            _textureRect.Visible = !_textureRect.Visible;
            UpdateButtonText();
        }

        private void UpdateButtonText()
        {
            _button.Text = _textureRect.Visible ? "HIDE" : "SHOW";
        }

        public override void _EnterTree()
        {
            GetNodes();
        }
    }
}