# Canvas.TestRunner - CLAUDE.md

This file provides guidance to Claude Code when working with the Canvas.TestRunner project.

## Project Overview

Canvas.TestRunner is a WPF-based visual test automation framework that allows users to create, edit, and execute test flows through a graphical interface. It's part of the FlaUI solution and is designed to provide a user-friendly way to build UI automation test suites.

## Technology Stack

- **Framework**: .NET 8.0 (Windows-only)
- **UI**: WPF with WPF-UI library (v4.0.2) for modern Fluent design
- **MVVM**: CommunityToolkit.Mvvm (v8.4.0) for observable objects and commands
- **Serialization**: DataContract XML serialization for project and flow persistence
- **Architecture**: MVVM (Model-View-ViewModel) pattern

## Project Structure

### Core Components

```
Canvas.TestRunner/
├── Model/                          # Business logic and data models
│   ├── Actions/                    # Action implementations
│   │   ├── Logic/                  # Logic actions (If, While, Variables, etc.)
│   │   │   ├── Conditions/         # Conditional actions (IfAction)
│   │   │   ├── Loops/              # Loop actions (WhileLoopAction, ForLoopAction)
│   │   │   ├── Variable/           # Variable management (SetVariableAction)
│   │   │   ├── Expression/         # Expression evaluation (ComparationExpression, ArithmeticalExpression)
│   │   │   └── Environment.cs      # Variable storage and scope management
│   │   ├── FlowControl/            # (Planned) Flow control actions
│   │   ├── UIAutomation/           # (Planned) UI automation actions
│   │   ├── File/                   # (Planned) File operations
│   │   ├── Folder/                 # (Planned) Folder operations
│   │   ├── HTTP/                   # (Planned) HTTP/API actions
│   │   └── Scripts/                # (Planned) Script execution
│   ├── Flow/                       # Test flow management
│   │   ├── Flow.cs                 # TestFlow - main execution container
│   │   ├── FlowValidator.cs        # Syntax validation logic
│   │   ├── FlowValidationSummary.cs # Validation results
│   │   ├── SyntaxError.cs          # Error definitions
│   │   └── TestFlowXmlSerializer.cs # Flow serialization
│   ├── Project/                    # Project management
│   │   └── TestProject.cs          # Project container for multiple TestFlows
│   ├── IAction.cs                  # Base action interface
│   └── ActionTemplate.cs           # Action template for toolbox
├── ViewModel/                      # View models (MVVM)
│   ├── MainViewModel.cs            # Main window view model
│   ├── ObservableProject.cs        # Observable wrapper for TestProject
│   └── ObservableFlow.cs           # Observable wrapper for TestFlow
├── Services/                       # Business services
│   └── TestProjectManager.cs       # Project lifecycle management (CRUD operations)
├── Dialogs/                        # Dialog windows
│   ├── ProjectNameDialog.xaml      # New project dialog
│   └── TestFlowNameDialog.xaml     # New test flow dialog
├── Utilities/                      # Helper classes
│   └── ProjectOperations.cs        # Project utility functions
├── Examples/                       # Example/documentation code
├── MainWindow.xaml                 # Main application window
└── App.xaml                        # Application entry point
```

## Architecture Patterns

### 1. MVVM Pattern

The project strictly follows the MVVM pattern:

- **Models** (`Model/`): Core business logic, no UI dependencies
  - `TestProject`: Container for test flows with file management
  - `TestFlow`: Execution container with action orchestration
  - `IAction`: Base interface for all executable actions

- **ViewModels** (`ViewModel/`): UI-aware wrappers with property change notifications
  - `MainViewModel`: Orchestrates UI commands and manages project state
  - `ObservableProject`: Observable wrapper that syncs with TestProject model
  - `ObservableFlow`: Observable wrapper that syncs with TestFlow model

- **Views** (`.xaml` files): Pure UI, binds to ViewModels
  - Uses WPF data binding for all UI updates
  - No code-behind logic except event handlers

### 2. Observable Wrapper Pattern

ViewModels wrap models to provide:
- `INotifyPropertyChanged` for data binding
- Two-way synchronization between observable and model
- `RefreshFromModel()`: Pulls changes from model to observable
- `SyncToModel()`: Pushes changes from observable to model

Example:
```csharp
// ObservableFlow wraps TestFlow
public class ObservableFlow : ObservableObject
{
    private readonly TestFlow _model;
    public TestFlow Model => _model;

    public void SyncToModel() { /* sync properties */ }
    public void RefreshFromModel() { /* sync properties */ }
}
```

### 3. Action Pattern

All executable operations implement `IAction`:
```csharp
public interface IAction
{
    string Name { get; }
    string Description { get; }
    ActionState State { get; set; }
    bool IsEnabled { get; }
    Task<bool> ExecuteAsync(CancellationToken cancellationToken);
    void Cancel();
}
```

Specialized interfaces:
- `ILogicAction`: Actions that require variable environment (If, While, etc.)
- `IAction<TResult>`: Actions that return typed results

### 4. Service Pattern

`TestProjectManager` is a service class that:
- Manages project lifecycle (create, open, save, close)
- Handles file I/O operations
- Fires events for UI notifications
- Prompts for user decisions via event callbacks
- Separates business logic from UI concerns

### 5. Serialization Strategy

- **Project Files** (`.testproj`): Contain project metadata and references to TestFlow files
- **TestFlow Files** (`.testflow`): Contain individual test flow definitions with actions
- Uses `DataContract` and `DataMember` attributes for XML serialization
- Non-serializable properties (like `CancellationTokenSource`) are marked with `IgnoreDataMember` or reconstructed after deserialization

## Key Concepts

### TestProject
- Container for multiple TestFlows
- Manages TestFlow file references (`TestFlowFileNames` property)
- Provides methods: `AddTestFlow()`, `RemoveTestFlow()`, `FindTestFlowByName()`
- Handles loading/saving TestFlows from/to separate files

### TestFlow
- Execution container for a sequence of actions
- Has states: Ready, Running, Paused, Completed, Failed
- Provides execution control: `ExecuteAsync()`, `Cancel()`
- Contains validation logic: `ValidateSyntax()`, `HasSyntaxErrors()`
- Maintains an `Environment` for variable storage

### Actions
Current implementations:
- **IfAction**: Conditional execution based on boolean expressions
- **WhileLoopAction**: Loop while condition is true
- **ForLoopAction**: Loop with initialization, condition, and increment
- **SetVariableAction**: Assign values to variables

Planned categories (folders exist but not implemented):
- UI Automation, File/Folder operations, HTTP, Scripts, etc.

### Environment
- Stores variables for logic actions
- Provides variable scope management
- Used by `ILogicAction` implementations

## File Operations

### Project Persistence
1. **Save Project**:
   - Saves `.testproj` file with project metadata
   - Saves each TestFlow to separate `.testflow` files
   - Creates `TestFlows/` directory relative to project file

2. **Load Project**:
   - Loads `.testproj` file
   - Reads `TestFlowFileNames` array
   - Loads each TestFlow from its file
   - Initializes runtime objects after deserialization

### TestFlow Persistence
- Each TestFlow is saved to a separate XML file
- TestProject only stores file references, not full TestFlow objects
- Files are typically stored in `TestFlows/` subdirectory

## Commands (MVVM)

MainViewModel exposes these commands:
- `CreateNewProjectCommand`: Creates new project with user prompt
- `OpenProjectCommand`: Opens existing project file
- `SaveProjectCommand`: Saves current project (requires file path)
- `SaveProjectAsCommand`: Saves to new location
- `CloseProjectCommand`: Closes project (prompts to save if unsaved)

## Event-Driven UI

The ViewModel communicates with the View through events:
- `OnPromptProjectName`: Request user input for new project
- `OnShowError`: Display error message
- `OnShowInfo`: Display information message
- `OnPromptSaveAsDialog`: Show "Save As" file dialog
- `OnPromptOpenDialog`: Show "Open" file dialog

These events are wired up in `MainWindow.xaml.cs` `SetupUIEventHandlers()`.

## Validation System

TestFlow provides syntax validation:
```csharp
// Validate and get errors
var errors = testFlow.ValidateSyntax();
bool hasErrors = testFlow.HasSyntaxErrors();
var summary = testFlow.GetValidationSummary();
```

Error severity levels:
- `Critical`: Blocks execution
- `Error`: Serious issues
- `Warning`: Potential problems

## Building and Running

### Build
```bash
dotnet build Canvas.TestRunner/Canvas.TestRunner.csproj
dotnet build Canvas.TestRunner/Canvas.TestRunner.csproj -c Release
```

### Run
```bash
dotnet run --project Canvas.TestRunner/Canvas.TestRunner.csproj
```

## Current Development Status

Based on git status, recent work includes:
- Implementation of TestProject and ObservableProject models
- Integration of TestFlow serialization with project files
- UI dialogs for project and test flow creation
- Action toolbox with logic actions (If, While, For, SetVariable)
- ViewModel layer with MVVM commands
- TestProjectManager service for project lifecycle

## Design Decisions

### Why Separate TestFlow Files?
- **Modularity**: Each test flow is independent
- **Version Control**: Easier to track changes to individual flows
- **Reusability**: TestFlows could potentially be shared across projects
- **Performance**: Load only needed flows (future optimization)

### Why Observable Wrappers?
- **Separation of Concerns**: Models remain UI-agnostic
- **Data Binding**: ViewModels implement `INotifyPropertyChanged` for WPF
- **Testability**: Models can be tested without UI dependencies
- **Flexibility**: Can have multiple ViewModels for same Model

### Why Event-Based UI Communication?
- **Decoupling**: ViewModel doesn't depend on specific UI implementations
- **Testability**: Can test ViewModel without actual UI
- **Flexibility**: Can swap different UI implementations (dialogs, notifications)

## Common Tasks

### Adding a New Action Type
1. Create action class implementing `IAction` or `ILogicAction`
2. Add serialization attributes (`[DataContract]`, `[DataMember]`)
3. Implement `ExecuteAsync()` logic
4. Add to `MainViewModel.InitializeAvailableActions()` for toolbox
5. Handle in `MainViewModel.CreateActionInstance()`

### Adding a New Action Category
1. Create folder in `Model/Actions/`
2. Implement action classes in the folder
3. Add category to toolbox in `InitializeAvailableActions()`
4. Update this documentation

### Modifying Project File Format
1. Update `TestProject` class properties
2. Add `[DataMember]` attributes for new properties
3. Update XML serialization in `Utilities/ProjectOperations.cs`
4. Handle backward compatibility for old project files

### Adding UI Features
1. Create or modify XAML view
2. Add properties/commands to ViewModel
3. Wire up data bindings in XAML
4. Add event handlers in code-behind if needed
5. Update MainViewModel to handle new events

## Dependencies

### NuGet Packages
- **CommunityToolkit.Mvvm** (v8.4.0): MVVM helpers, observable objects, relay commands
- **WPF-UI** (v4.0.2): Modern Fluent Design controls for WPF

### Project References
Currently none - Canvas.TestRunner is standalone within the FlaUI solution. Future plans may include integration with FlaUI.Core for UI automation actions.

## Future Enhancements (Planned Folders)

Based on folder structure, planned features include:
- UI Automation actions (using FlaUI.Core)
- File and folder operations
- HTTP/REST API actions
- FTP operations
- Compression/decompression
- Date/time operations
- Script execution (PowerShell, Python, etc.)
- Local machine operations
- Text manipulation

## Best Practices

1. **Always use Observable wrappers in UI code**: Never bind directly to Model objects
2. **Sync before save**: Call `SyncToModel()` on ObservableFlows before saving
3. **Initialize after load**: Call `InitializeAfterDeserialization()` on TestFlows after loading
4. **Handle cancellation**: All ExecuteAsync implementations should respect CancellationToken
5. **Validate file paths**: Always check/create directories before saving files
6. **Use events for UI interaction**: Don't call UI methods directly from ViewModels
7. **Keep Models UI-agnostic**: Models should have no WPF dependencies

## Troubleshooting

### Project won't save
- Check that `CurrentProjectFilePath` is set
- Ensure directory exists and is writable
- Verify all TestFlows have valid `FileName` properties

### TestFlow execution hangs
- Check for missing cancellation token handling in actions
- Verify no infinite loops in While/For actions
- Ensure all actions properly implement `ExecuteAsync`

### Serialization errors
- Verify all properties have `[DataMember]` attributes
- Check that complex types are serializable
- Ensure non-serializable fields are excluded
- Call `InitializeAfterDeserialization()` after loading

### Data binding not working
- Verify ViewModel properties have `[ObservableProperty]` attribute
- Check that DataContext is set correctly in XAML
- Ensure property names match between XAML and ViewModel
- Use `OnPropertyChanged()` when needed

## Related Documentation

- FlaUI Core Documentation: For UI automation capabilities
- WPF-UI Documentation: https://wpfui.lepo.co/
- CommunityToolkit.Mvvm: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/

## Notes

- This is a work-in-progress project with many planned features
- The architecture supports extensibility for new action types
- The project follows modern WPF/MVVM best practices
- Current focus is on building core infrastructure before adding many action types
