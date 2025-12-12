# FleetAutomate

A visual, WYSIWYG automation test tool for Windows, built on FlaUI and inspired by Microsoft Power Automate.

## Overview

FleetAutomate provides a drag-and-drop interface for creating Windows UI automation test flows without writing code. Design complex test scenarios visually, execute them with a single click, and debug failures with detailed execution state tracking.

## Features

### Visual Flow Designer
- **Drag-and-Drop Actions**: Build test flows by dragging actions from the toolbox
- **TreeView Hierarchy**: Visualize nested actions and control flow
- **Real-Time Validation**: Syntax errors highlighted before execution
- **State Indicators**: See which actions are running, completed, or failed

### UI Automation
- **Element Capture**: Hover over any UI element and press Ctrl to capture it
- **Multiple Identification Methods**: XPath, AutomationId, Name, or ClassName
- **Smart Element Finding**: Optimized searches to prevent hangs
- **Click Actions**: Single-click, double-click, or invoke pattern support
- **Wait for Element**: Wait until elements appear with timeout support

### Logic & Control Flow
- **Conditional Logic**: If/Then/Else branches based on expressions
- **Loops**: While and For loops with break/continue support
- **Variables**: Set and use variables throughout your flows
- **Expressions**: Comparison, arithmetical, and literal expressions

### System Integration
- **Launch Applications**: Start processes and wait for them to be ready
- **File Operations**: (Planned) Read, write, copy, delete files
- **HTTP Requests**: (Planned) REST API integration
- **Script Execution**: (Planned) PowerShell, Python, and more

### Project Management
- **Multi-Flow Projects**: Organize related test flows in a single project
- **Separate Files**: Each test flow stored independently for better version control
- **Recent Projects**: Quick access to recently opened projects
- **Auto-Save**: Prompts to save unsaved changes

## Prerequisites

- **Windows 10/11**: Required for WPF and UI Automation
- **.NET 8.0 SDK**: [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Git**: For cloning the repository

## Quick Start

### Installation

1. Clone the repository with submodules:
```bash
git clone --recurse-submodules https://github.com/yourusername/FleetAutomate.git
cd FleetAutomate
```

2. Build the application:
```bash
dotnet build src/FleetAutomate.Application/FleetAutomate.csproj
```

3. Run FleetAutomate:
```bash
dotnet run --project src/FleetAutomate.Application/FleetAutomate.csproj
```

### Creating Your First Test Flow

1. **Create a New Project**
   - Click "New Project" or press `Ctrl+N`
   - Enter a project name
   - Choose a save location

2. **Add a Test Flow**
   - Click "Add Test Flow"
   - Give it a descriptive name

3. **Build Your Flow**
   - Drag actions from the toolbox on the left
   - Configure each action by double-clicking it
   - Use the element capture feature (hover + Ctrl) to identify UI elements

4. **Run Your Flow**
   - Select the test flow
   - Click "Run" or press `F5`
   - Watch the execution in real-time

## Project Structure

```
FleetAutomate/
├── src/
│   ├── FleetAutomate.Application/     # Main WPF application
│   │   ├── Model/                     # Business logic (TestProject, TestFlow, Actions)
│   │   ├── ViewModel/                 # MVVM ViewModels
│   │   ├── Services/                  # Services (ProjectManager, ElementCapture)
│   │   ├── Dialogs/                   # Dialog windows
│   │   ├── Converters/                # WPF value converters
│   │   ├── Utilities/                 # Helper classes
│   │   └── MainWindow.xaml            # Main application window
│   └── FlaUI/                         # Git submodule: FlaUI library
│       └── src/
│           ├── FlaUI.Core/            # Core automation library
│           ├── FlaUI.UIA2/            # UIA2 provider
│           └── FlaUI.UIA3/            # UIA3 provider
├── CLAUDE.md                          # AI assistant guidance
├── LICENSE                            # License information
└── README.md                          # This file
```

## Building from Source

### Debug Build
```bash
dotnet build src/FleetAutomate.Application/FleetAutomate.csproj
```

### Release Build
```bash
dotnet build src/FleetAutomate.Application/FleetAutomate.csproj -c Release
```

### Clean Build
```bash
dotnet clean src/FleetAutomate.Application/FleetAutomate.csproj
dotnet build src/FleetAutomate.Application/FleetAutomate.csproj
```

## File Formats

### `.testproj` Files
Project files containing:
- Project name and metadata
- References to `.testflow` files
- Project-level settings

### `.testflow` Files
Test flow files containing:
- Flow name and description
- Sequence of actions
- Variable definitions
- Execution state

Both formats use XML serialization for human-readability and version control friendliness.

## Action Types

### Currently Implemented

**Logic**
- `If Action`: Conditional branching with Then/Else blocks
- `While Loop`: Repeat while condition is true
- `For Loop`: Iterate with counter
- `Set Variable`: Assign values to variables

**UI Automation**
- `Click Element`: Click UI elements by XPath, ID, Name, or ClassName
- `Wait for Element`: Wait until element appears or timeout

**System**
- `Launch Application`: Start executables and wait for them to be ready

### Planned (Folders Created)

- **File Operations**: Read, write, copy, move, delete
- **Folder Operations**: Create, delete, enumerate
- **HTTP**: REST API calls with JSON/XML support
- **FTP**: Upload, download, list files
- **Compression**: Zip, unzip archives
- **Date/Time**: Date arithmetic, formatting, parsing
- **Text**: String manipulation, regex matching
- **Scripts**: PowerShell, Python, batch script execution

## Keyboard Shortcuts

- `Ctrl+N`: New Project
- `Ctrl+O`: Open Project
- `Ctrl+S`: Save Project
- `F5`: Run selected test flow
- `Ctrl`: (Hover over UI element) Capture element for automation

## Architecture

FleetAutomate uses a clean MVVM architecture:

- **Models**: Pure business logic, no UI dependencies
- **ViewModels**: Observable wrappers for data binding
- **Views**: WPF XAML with modern Fluent design (WPF-UI)
- **Services**: Separated concerns (project management, element capture, XPath generation)

For detailed architectural documentation, see [CLAUDE.md](CLAUDE.md).

## Technology Stack

- **.NET 8.0**: Modern C# with latest language features
- **WPF**: Windows Presentation Foundation for UI
- **WPF-UI**: Modern Fluent Design controls
- **CommunityToolkit.Mvvm**: MVVM helpers and source generators
- **FlaUI**: Powerful UI automation framework (UIA2 + UIA3)

## Contributing

Contributions are welcome! Areas where help is needed:

1. **New Action Types**: Implement planned actions (File, HTTP, etc.)
2. **UI Improvements**: Enhanced visual design and user experience
3. **Documentation**: Tutorials, examples, and guides
4. **Bug Fixes**: Report and fix issues
5. **Testing**: Create test projects and report edge cases

### Development Workflow

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes following the existing architecture
4. Test thoroughly
5. Commit your changes (`git commit -m 'Add amazing feature'`)
6. Push to the branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

## Known Limitations

- **Windows Only**: Requires Windows for WPF and UI Automation APIs
- **Administrator Rights**: Some applications require elevated privileges to automate
- **Timing Issues**: Complex UIs may need explicit waits
- **Security Software**: Antivirus/EDR may block automation in protected applications

## Troubleshooting

### Element Not Found
- Try different identifier types (XPath, Name, AutomationId)
- Add a "Wait for Element" action before clicking
- Use the element capture feature to verify the path
- Check if the element is in a different window

### Access Denied Errors
- Run FleetAutomate as Administrator
- Check if target application has higher privileges
- Some system UI elements cannot be automated

### Application Hangs
- Avoid XPath searches from desktop level (optimized window search is used automatically)
- Add cancellation support to long-running actions
- Check for infinite loops in While actions

## Roadmap

- [ ] **Expression Builder UI**: Visual expression editor
- [ ] **Debugger**: Step-through execution with breakpoints
- [ ] **Test Reporting**: HTML/XML test reports
- [ ] **CI/CD Integration**: Command-line runner for automated testing
- [ ] **Action Marketplace**: Share and download community actions
- [ ] **Recording Mode**: Record user interactions as test flows
- [ ] **Data-Driven Testing**: CSV/Excel data sources for test parameters
- [ ] **Screenshot Capture**: Automatic screenshots on failure
- [ ] **Performance Metrics**: Track execution times and bottlenecks

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- **FlaUI**: Excellent UI automation framework for .NET
- **WPF-UI**: Modern and beautiful WPF controls
- **CommunityToolkit**: Essential MVVM utilities
- **Microsoft Power Automate**: Inspiration for visual flow design

## Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/FleetAutomate/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/FleetAutomate/discussions)

---

**Made with ❤️ for the Windows automation community**
