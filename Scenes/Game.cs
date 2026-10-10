using Godot;
using System;
using System.Collections.Generic;

public partial class Game : Control
{
	public static Game Instance { get; private set; }

	Reticle Reticle;

	public enum State
	{
		OBSERVING,
		MAIN_MENU,
		SCULPTING,
		BUILDING,
		DEBUGGER
	}

	public struct StateData(string title, State state, Image icon)
	{
		public string Title = title;
		public State State = state;
		public Image Icon = icon;
	};

	public State GameState = State.OBSERVING;

	private Dictionary<State, StateData[]> _transitions;

	private void SwitchState(State nextState)
	{
		GameState = nextState;
	}

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public void SwitchStateFromReticle(int slot)
	{
		StateData stateData = _transitions[GameState][slot];

		GD.Print(stateData.Title, stateData.State);
		SwitchState(stateData.State);
	}


	public override void _Ready()
	{
		Reticle = GetNode<Reticle>("%Reticle");
		Reticle.ReticleOptionSelected += SwitchStateFromReticle;
		_transitions = [];
		
		_transitions[State.OBSERVING] = [
			new StateData(
				"Sculpt Mode",
				State.SCULPTING,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
			new StateData(
				"Main Menu",
				State.MAIN_MENU,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
			new StateData(
				"Building",
				State.BUILDING,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
			new StateData(
				"Debug Menu",
				State.DEBUGGER,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
		];
		
		_transitions[State.SCULPTING] = [
			new StateData(
				"Observe Mode",
				State.OBSERVING,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
			new StateData(
				"Main Menu",
				State.MAIN_MENU,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
		];

		_transitions[State.BUILDING] = [
			new StateData(
				"Observe Mode",
				State.OBSERVING,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
			new StateData(
				"Main Menu",
				State.MAIN_MENU,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
		];

		_transitions[State.MAIN_MENU] = [
			new StateData(
				"Play Game",
				State.OBSERVING,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
		];


		_transitions[State.DEBUGGER] = [
			new StateData(
				"Observe Mode",
				State.OBSERVING,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
			new StateData(
				"Main Menu",
				State.MAIN_MENU,
				Image.CreateEmpty(128, 128, false, Image.Format.Rgb8)
			),
		];

			// Title: [
			// 	"A",
			// 	"B",
			// 	"C",
			// 	"D"
			// ],
			// Actions:
			// [
			// 	// Draw Mode
			// 	() => { SwitchState(State.DRAWING); },
			// 	// Quit Game
			// 	() => { SwitchState(State.MAIN_MENU); },
			// 	// Place Grids
			// 	() => { GD.Print("Sorry Not Implemented."); },
			// 	// Debug Menu
			// 	() => { SwitchState(State.DEBUGGER); }
			// ],
			// Icons: []
			// )
		
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true } mouse)
		{
			// GD.Print($"Clicked: {GetPath()}");
			// GD.Print($"Mouse Filter: {MouseFilter}");
		}
	}
}
