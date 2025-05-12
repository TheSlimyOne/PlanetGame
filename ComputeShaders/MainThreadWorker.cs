using Godot;

public abstract partial class MainThreadWorker : Node
{
    private bool _isInitialized = false;

    // // Constructor for programmatic instantiation
    // protected MainThreadWorker(params object[] parameters)
    // {
    //     Initialize(parameters);
    // }

    // // Godot _Ready for scene instantiation
    // public override void _Ready()
    // {
    //     if (!_isInitialized)
    //         Initialize();
    // }

    // // Initialization logic, called by both constructor and _Ready
    // private void Initialize(params object[] parameters)
    // {
    //     GD.Print($"{GetType().Name} Initialized with parameters: {string.Join(", ", parameters)}");
    //     _isInitialized = true;
    //     OnInitialized(parameters);
    // }

    // public abstract void BeginCompute();
    // public abstract void EndCompute();
    // public abstract void UpdateCompute();
    protected abstract void InitializeGPUResources(params object[] parameters);
    protected abstract void CleanupGPUResources();
    public abstract void Invoke();
}