using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace PlanetGame.Util.DebugUIComponents
{
    public partial class SetValueComponent : PanelContainer, IDebugComponent
    {
        public enum ValueType
        {
            Int,
            UInt,
            Float
        }

        public class SetValueBinding<T>
        {
            public Func<T> GetState { get; }
            public Action<T> Action { get; }
            public T Step { get; }
            public Func<T, T> SubtractFunction { get; }
            public Func<T, T> AddFunction { get; }

            public SetValueBinding(Func<T> getState, Action<T> action, T step, Func<T, T> subtractFunction = null, Func<T, T> addFunction = null)
            {
                GetState = getState;
                Action = action;
                Step = step;
                SubtractFunction = subtractFunction;
                AddFunction = addFunction;
            }
        }

        private class SetValueEntry
        {
            public HBoxContainer Container;
            public Button SubtractButton;
            public LineEdit ValueField;
            public Button AddButton;
            public Action<double> Action;
            public Func<double, double> SubtractFunction;
            public Func<double, double> AddFunction;
        }

        private class InternalBinding
        {
            public Func<double> GetState;
            public Action<double> Action;
            public double Step;
            public Func<double, double> SubtractFunction;
            public Func<double, double> AddFunction;
        }

        public string TechnicalName { get; set; }
        public bool IsTemplate { get; set; }

        private Label _label;
        private VBoxContainer _setValueRows;
        private HBoxContainer _setValueRowTemplate;

        private ValueType _valueType;
        private readonly List<SetValueEntry> _setValueEntries = new();

        public void GetNodes()
        {
            _label = GetNode<Label>("%Label");
            _setValueRows = GetNode<VBoxContainer>("%SetValueRows");
            _setValueRowTemplate = _setValueRows.GetNode<HBoxContainer>("SetValueRowTemplate");
        }

        public void Initialize(string name, SetValueBinding<int>[] bindings, bool isTemplate = false)
        {
            Initialize(name, CreateBindings(bindings), ValueType.Int, isTemplate);
        }

        public void Initialize(string name, SetValueBinding<uint>[] bindings, bool isTemplate = false)
        {
            Initialize(name, CreateBindings(bindings), ValueType.UInt, isTemplate);
        }

        public void Initialize(string name, SetValueBinding<float>[] bindings, bool isTemplate = false)
        {
            Initialize(name, CreateBindings(bindings), ValueType.Float, isTemplate);
        }

        public void Initialize(string name, Func<int> getState, Action<int> action, int step = 1, Func<int, int> subtractFunction = null, Func<int, int> addFunction = null, bool isTemplate = false)
        {
            Initialize(name, [new SetValueBinding<int>(getState, action, step, subtractFunction, addFunction)], isTemplate);
        }

        public void Initialize(string name, Func<uint> getState, Action<uint> action, uint step = 1, Func<uint, uint> subtractFunction = null, Func<uint, uint> addFunction = null, bool isTemplate = false)
        {
            Initialize(name, [new SetValueBinding<uint>(getState, action, step, subtractFunction, addFunction)], isTemplate);
        }

        public void Initialize(string name, Func<float> getState, Action<float> action, float step = 1.0f, Func<float, float> subtractFunction = null, Func<float, float> addFunction = null, bool isTemplate = false)
        {
            Initialize(name, [new SetValueBinding<float>(getState, action, step, subtractFunction, addFunction)], isTemplate);
        }

        public void InitializeTemplate(string name, int step = 1)
        {
            InitializeTemplate(name, step, ValueType.Int);
        }

        public void InitializeTemplate(string name, uint step = 1)
        {
            InitializeTemplate(name, step, ValueType.UInt);
        }

        public void InitializeTemplate(string name, float step = 1.0f)
        {
            InitializeTemplate(name, step, ValueType.Float);
        }

        private void Initialize(string name, InternalBinding[] bindings, ValueType valueType, bool isTemplate)
        {
            GetNodes();

            TechnicalName = name.ToCamelCase();
            IsTemplate = isTemplate;
            _valueType = valueType;

            Name = $"{TechnicalName}SetValueComponent";
            _label.Text = name;

            CreateSetValueRows(bindings);
        }

        private void InitializeTemplate(string name, double step, ValueType valueType)
        {
            GetNodes();

            TechnicalName = name.ToCamelCase();
            IsTemplate = true;
            _valueType = valueType;

            Name = $"{TechnicalName}SetValueComponent";
            _label.Text = name;

            ConfigureTemplateRow();
        }

        private void CreateSetValueRows(InternalBinding[] bindings)
        {
            _setValueEntries.Clear();

            if (bindings == null || bindings.Length == 0)
                return;

            for (int index = 0; index < bindings.Length; index++)
            {
                HBoxContainer row;

                if (index == 0)
                    row = _setValueRowTemplate;
                else
                {
                    row = (HBoxContainer)_setValueRowTemplate.Duplicate();
                    _setValueRows.AddChild(row);
                }

                row.Name = $"SetValueRow{index}";

                SetValueEntry entry = CreateSetValueEntry(row, bindings[index]);
                _setValueEntries.Add(entry);
            }
        }

        private SetValueEntry CreateSetValueEntry(HBoxContainer row, InternalBinding binding)
        {
            Button subtractButton = row.GetNode<Button>("SubtractButton");
            LineEdit valueField = row.GetNode<LineEdit>("ValueLabel");
            Button addButton = row.GetNode<Button>("AddButton");

            double state = 0.0;

            if (binding.GetState != null)
                state = binding.GetState();

            valueField.Text = FormatValue(state);

            return new SetValueEntry
            {
                Container = row,
                SubtractButton = subtractButton,
                ValueField = valueField,
                AddButton = addButton,
                Action = binding.Action,
                SubtractFunction = binding.SubtractFunction,
                AddFunction = binding.AddFunction
            };
        }

        private void ConfigureTemplateRow()
        {
            _setValueRowTemplate.Name = "SetValueRow0";
            _setValueRowTemplate.GetNode<LineEdit>("ValueLabel").Text = FormatValue(0.0);
        }

        private readonly Dictionary<SetValueEntry, (Action Subtract, Action Add, LineEdit.TextSubmittedEventHandler Submit, Action FocusExit, LineEdit.TextSubmittedEventHandler Release)> _handlers = [];

        public override void _EnterTree()
        {
            if (IsTemplate) return;

            foreach (SetValueEntry entry in _setValueEntries)
            {
                void subtract() => OnSubtractPressed(entry);
                void add() => OnAddPressed(entry);
                void submit(string _) => OnValueSubmitted(entry);
                void focusExit() => OnValueSubmitted(entry);
                void release(string _) => entry.ValueField.ReleaseFocus();

                entry.SubtractButton.Pressed += subtract;
                entry.AddButton.Pressed += add;
                entry.ValueField.TextSubmitted += submit;
                entry.ValueField.FocusExited += focusExit;
                entry.ValueField.TextSubmitted += release;

                _handlers[entry] = (subtract, add, submit, focusExit, release);
            }
        }

        public override void _ExitTree()
        {
            if (IsTemplate) return;

            foreach ((SetValueEntry entry, var handlers) in _handlers)
            {
                entry.SubtractButton.Pressed -= handlers.Subtract;
                entry.AddButton.Pressed -= handlers.Add;
                entry.ValueField.TextSubmitted -= handlers.Submit;
                entry.ValueField.FocusExited -= handlers.FocusExit;
                entry.ValueField.TextSubmitted -= handlers.Release;
            }

            _handlers.Clear();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
                return;

            foreach (SetValueEntry entry in _setValueEntries)
            {
                if (!entry.ValueField.GetGlobalRect().HasPoint(mouse.GlobalPosition))
                    entry.ValueField.ReleaseFocus();
            }
        }

        private void OnSubtractPressed(SetValueEntry entry)
        {
            if (!TryParseValue(entry.ValueField.Text, out double value))
                return;

            SetValue(entry, entry.SubtractFunction(value));
        }

        private void OnAddPressed(SetValueEntry entry)
        {
            if (!TryParseValue(entry.ValueField.Text, out double value))
                return;

            SetValue(entry, entry.AddFunction(value));
        }

        private void OnValueSubmitted(SetValueEntry entry)
        {
            if (!TryParseValue(entry.ValueField.Text, out double value))
                return;

            SetValue(entry, value);
        }

        private void SetValue(SetValueEntry entry, double value)
        {
            entry.Action?.Invoke(value);
            entry.ValueField.Text = FormatValue(value);
        }

        private bool TryParseValue(string text, out double value)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;

            switch (_valueType)
            {
                case ValueType.Int:
                    value = (int)value;
                    break;

                case ValueType.UInt:
                    value = Math.Max(value, 0);
                    value = (uint)value;
                    break;

                case ValueType.Float:
                    value = (float)value;
                    break;
            }

            return true;
        }

        private string FormatValue(double value)
        {
            return _valueType switch
            {
                ValueType.Int => ((int)value).ToString(),
                ValueType.UInt => ((uint)Math.Max(value, 0)).ToString(),
                ValueType.Float => ((float)value).ToString("0.##", CultureInfo.InvariantCulture),
                _ => value.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static InternalBinding[] CreateBindings(SetValueBinding<int>[] bindings)
        {
            if (bindings == null) return null;

            InternalBinding[] result = new InternalBinding[bindings.Length];

            for (int index = 0; index < bindings.Length; index++)
            {
                SetValueBinding<int> binding = bindings[index];

                result[index] = new InternalBinding
                {
                    GetState = binding.GetState != null ? () => binding.GetState() : null,
                    Action = binding.Action != null ? value => binding.Action((int)value) : null,
                    Step = binding.Step,
                    SubtractFunction = binding.SubtractFunction != null ? value => binding.SubtractFunction((int)value) : value => value - binding.Step,
                    AddFunction = binding.AddFunction != null ? value => binding.AddFunction((int)value) : value => value + binding.Step
                };
            }

            return result;
        }

        private static InternalBinding[] CreateBindings(SetValueBinding<uint>[] bindings)
        {
            if (bindings == null) return null;

            InternalBinding[] result = new InternalBinding[bindings.Length];

            for (int index = 0; index < bindings.Length; index++)
            {
                SetValueBinding<uint> binding = bindings[index];

                result[index] = new InternalBinding
                {
                    GetState = binding.GetState != null ? () => binding.GetState() : null,
                    Action = binding.Action != null ? value => binding.Action((uint)value) : null,
                    Step = binding.Step,
                    SubtractFunction = binding.SubtractFunction != null ? value => binding.SubtractFunction((uint)value) : value => Math.Max(value - binding.Step, 0),
                    AddFunction = binding.AddFunction != null ? value => binding.AddFunction((uint)value) : value => value + binding.Step
                };
            }

            return result;
        }

        private static InternalBinding[] CreateBindings(SetValueBinding<float>[] bindings)
        {
            if (bindings == null) return null;

            InternalBinding[] result = new InternalBinding[bindings.Length];

            for (int index = 0; index < bindings.Length; index++)
            {
                SetValueBinding<float> binding = bindings[index];

                result[index] = new InternalBinding
                {
                    GetState = binding.GetState != null ? () => binding.GetState() : null,
                    Action = binding.Action != null ? value => binding.Action((float)value) : null,
                    Step = binding.Step,
                    SubtractFunction = binding.SubtractFunction != null ? value => binding.SubtractFunction((float)value) : value => value - binding.Step,
                    AddFunction = binding.AddFunction != null ? value => binding.AddFunction((float)value) : value => value + binding.Step
                };
            }

            return result;
        }
    }
}
