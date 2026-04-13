from __future__ import annotations

import argparse
import json
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Callable


def _default_python() -> str:
    return r"C:\Users\wenqu\AppData\Local\Programs\Python\Python313\python.exe"


def _default_recent_projects_file() -> Path:
    appdata = Path.home() / "AppData" / "Roaming"
    return appdata / "Canvas.TestRunner" / "recent-projects.json"


def _default_exe() -> Path:
    return (
        Path(__file__).resolve().parents[2]
        / "FleetAutomate.Application"
        / "bin"
        / "Debug"
        / "net8.0-windows"
        / "FleetAutomate.exe"
    )


def _default_winauto_cli_repo() -> Path:
    return Path(r"D:\work\winauto-cli")


def _local_name(tag: str) -> str:
    if "}" in tag:
        return tag.rsplit("}", 1)[1]
    return tag


def _find_child(element: ET.Element, name: str) -> ET.Element | None:
    for child in list(element):
        if _local_name(child.tag) == name:
            return child
    return None


def _read_recent_project(recent_projects_file: Path) -> dict:
    recent_projects = json.loads(recent_projects_file.read_text(encoding="utf-8"))
    if not recent_projects:
        raise RuntimeError(f"No recent projects found in {recent_projects_file}")
    return recent_projects[0]


def _load_xml_root(xml_path: Path) -> ET.Element:
    raw = xml_path.read_bytes()
    text = raw.decode("utf-8-sig")
    if 'encoding="utf-16"' in text:
        text = text.replace('encoding="utf-16"', 'encoding="utf-8"', 1)
    return ET.fromstring(text)


def _read_project_metadata(project_path: Path) -> tuple[str, Path, int]:
    project_root = _load_xml_root(project_path)

    test_flow_names = _find_child(project_root, "TestFlowFileNames")
    if test_flow_names is None:
        raise RuntimeError(f"TestFlowFileNames not found in {project_path}")

    first_flow_relative = None
    for child in list(test_flow_names):
        text = (child.text or "").strip()
        if text:
            first_flow_relative = text
            break

    if not first_flow_relative:
        raise RuntimeError(f"No test flows listed in {project_path}")

    first_flow_path = project_path.parent / first_flow_relative

    flow_root = _load_xml_root(first_flow_path)

    name_element = _find_child(flow_root, "Name")
    flow_name = (name_element.text or "").strip() if name_element is not None else first_flow_path.stem

    actions_array = _find_child(flow_root, "ActionsArray")
    root_action_count = len(list(actions_array)) if actions_array is not None else 0
    return flow_name, first_flow_path, root_action_count


def _install_winauto_cli_import(winauto_cli_repo: Path) -> None:
    repo_str = str(winauto_cli_repo)
    if repo_str not in sys.path:
        sys.path.insert(0, repo_str)


def _retry(description: str, fn: Callable[[], object], timeout: float = 15.0, interval: float = 0.3) -> object:
    deadline = time.monotonic() + timeout
    last_error: Exception | None = None
    while time.monotonic() < deadline:
        try:
            value = fn()
            if value:
                return value
        except Exception as exc:
            last_error = exc
        time.sleep(interval)

    if last_error is not None:
        raise RuntimeError(f"{description} failed: {last_error}") from last_error
    raise RuntimeError(f"{description} timed out after {timeout:.1f}s")


class SmokeTester:
    def __init__(
        self,
        python_exe: Path,
        exe_path: Path,
        recent_projects_file: Path,
        winauto_cli_repo: Path,
        launch_wait_seconds: float,
    ) -> None:
        self.python_exe = python_exe
        self.exe_path = exe_path
        self.recent_projects_file = recent_projects_file
        self.winauto_cli_repo = winauto_cli_repo
        self.launch_wait_seconds = launch_wait_seconds
        self.app_process: subprocess.Popen[str] | None = None

        _install_winauto_cli_import(winauto_cli_repo)

        from pywinauto import Application  # noqa: WPS433
        from pywinauto import keyboard  # noqa: WPS433
        from pywinauto import Desktop  # noqa: WPS433
        from pywinauto.findwindows import ElementNotFoundError  # noqa: WPS433
        from winauto_cli.automation import safe_summary  # noqa: WPS433

        self.Application = Application
        self.keyboard = keyboard
        self.Desktop = Desktop
        self.ElementNotFoundError = ElementNotFoundError
        self.safe_summary = safe_summary
        self.desktop = Desktop(backend="uia")
        self.app = None
        self.main_window = None
        self.main_window_spec = None

    def log(self, message: str) -> None:
        print(f"[smoke] {message}", flush=True)

    def launch(self) -> None:
        if not self.exe_path.exists():
            raise FileNotFoundError(f"FleetAutomate executable not found: {self.exe_path}")

        self.log(f"Launching {self.exe_path}")
        self.app_process = subprocess.Popen([str(self.exe_path)])
        time.sleep(self.launch_wait_seconds)
        self.app = self.Application(backend="uia").connect(process=self.app_process.pid)
        self.main_window = _retry(
            "FleetAutomate main window",
            lambda: self._find_main_window(self.app_process.pid),
            timeout=20.0,
            interval=0.5,
        )
        self.main_window_spec = self.desktop.window(handle=self.main_window.handle)
        try:
            self.main_window.restore()
        except Exception:
            pass
        try:
            self.main_window.set_focus()
        except Exception:
            pass

    def cleanup(self) -> None:
        if self.main_window is not None:
            try:
                self.main_window.close()
                time.sleep(1.0)
            except Exception:
                pass

        if self.app_process is not None and self.app_process.poll() is None:
            self.app_process.terminate()
            try:
                self.app_process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.app_process.kill()

    def _find_main_window(self, process_id: int):
        candidates = []
        for window in self.desktop.windows(class_name="Window", process=process_id):
            try:
                rect = window.rectangle()
                texts = [child.window_text() for child in window.descendants(control_type="Text")]
                score = 1 if any(text.strip() == "Fleet Automate" for text in texts) else 0
                if rect.width() > 200 and rect.height() > 200:
                    score += 2
                if window.is_visible():
                    score += 2
                candidates.append((score, window))
            except Exception:
                continue

        if not candidates:
            return None

        best_score, best_window = max(candidates, key=lambda item: item[0])
        return best_window if best_score > 0 else None

    def _window_descendants(self, **kwargs):
        return self.main_window.descendants(**kwargs)

    def _first_descendant(self, *, root=None, **kwargs):
        container = root or self.main_window_spec
        try:
            return container.child_window(**kwargs).wrapper_object()
        except self.ElementNotFoundError:
            return None

    def _menu_items_by_name(self, name: str):
        hits = []
        for window in self.desktop.windows():
            try:
                descendants = window.descendants(control_type="MenuItem")
            except Exception:
                continue
            for item in descendants:
                value = item.window_text().strip()
                if value == name:
                    hits.append(item)
        return hits

    def _menu_item_in_main_window(self, title: str):
        matches = []
        for item in self._window_descendants(control_type="MenuItem"):
            if item.window_text().strip() == title:
                matches.append(item)
        return matches[0] if matches else None

    def _invoke_or_select(self, element) -> None:
        try:
            element.invoke()
            return
        except Exception:
            pass
        try:
            element.select()
            return
        except Exception:
            pass
        try:
            element.set_focus()
        except Exception:
            pass

    def open_most_recent_project(self, project_name: str) -> None:
        self.log(f"Opening recent project '{project_name}'")
        file_menu = self._first_descendant(title="File", control_type="MenuItem")
        if file_menu is None:
            raise RuntimeError("File menu not found")
        file_menu.expand()
        time.sleep(0.3)

        open_recent = _retry(
            "Find Open Recent menu item",
            lambda: self._menu_item_in_main_window("Open Recent"),
            timeout=5.0,
            interval=0.2,
        )
        open_recent.expand()
        time.sleep(0.5)

        project_item = _retry(
            "Locate project item in Open Recent submenu",
            lambda: self._menu_item_in_main_window(project_name),
            timeout=8.0,
            interval=0.2,
        )
        self._invoke_or_select(project_item)
        self.main_window.set_focus()
        self.keyboard.send_keys("{ENTER}")

    def wait_for_testflows_loaded(self, expected_min_count: int) -> object:
        self.log("Waiting for TestFlows list to load")

        def _get_items():
            list_view = self._first_descendant(auto_id="TestFlowsListView", control_type="List")
            if list_view is None:
                return None
            items = [child for child in list_view.children() if self.safe_summary(child).get("control_type") == "ListItem"]
            return items if len(items) >= expected_min_count else None

        return _retry("TestFlow list items", _get_items, timeout=20.0, interval=0.5)

    def assert_no_testflow_load_errors(self) -> None:
        list_view = self._first_descendant(auto_id="TestFlowsListView", control_type="List")
        if list_view is None:
            raise RuntimeError("TestFlows list view not found")
        bad_tokens = ("error", "failed", "missing")
        texts: list[str] = []
        for element in list_view.descendants():
            summary = self.safe_summary(element)
            value = str(summary.get("name") or summary.get("window_text") or "").strip()
            if value:
                texts.append(value)

        bad_hits = [text for text in texts if any(token in text.lower() for token in bad_tokens)]
        if bad_hits:
            raise RuntimeError(f"Detected failed TestFlow load markers: {bad_hits}")

    def open_first_testflow(self, expected_flow_name: str):
        list_items = self.wait_for_testflows_loaded(expected_min_count=1)
        first_item = list_items[0]
        self.log(f"Opening first TestFlow: expected '{expected_flow_name}'")
        # The app only wires TestFlow opening to the ListView mouse double-click event.
        first_item.double_click_input()

        def _tab_opened():
            name_hits = [
                element
                for element in self._window_descendants(control_type="Text")
                if element.window_text().strip() == expected_flow_name
            ]
            trees = self._window_descendants(control_type="Tree")
            return (name_hits, trees) if len(name_hits) >= 2 and len(trees) >= 2 else None

        _retry("Open first TestFlow tab", _tab_opened, timeout=15.0, interval=0.5)

    def get_action_tree(self):
        trees = self._window_descendants(control_type="Tree")
        non_toolbox = [tree for tree in trees if self.safe_summary(tree).get("automation_id") != "ActionsToolBox"]
        if not non_toolbox:
            raise RuntimeError("Action tree was not found after opening the TestFlow tab")
        return non_toolbox[0]

    def assert_action_tree_count(self, expected_count: int):
        action_tree = self.get_action_tree()

        def _root_items():
            children = [child for child in action_tree.children() if self.safe_summary(child).get("control_type") == "TreeItem"]
            return children if len(children) == expected_count else None

        return _retry(
            f"Action tree root item count == {expected_count}",
            _root_items,
            timeout=10.0,
            interval=0.5,
        )

    def assert_action_context_menu(self) -> None:
        action_tree = self.get_action_tree()
        root_items = [child for child in action_tree.children() if self.safe_summary(child).get("control_type") == "TreeItem"]
        if not root_items:
            raise RuntimeError("No root action item found in action tree")
        first_action = root_items[0]
        self.log("Opening first action context menu")
        self._invoke_or_select(first_action)
        time.sleep(0.2)
        self.keyboard.send_keys("+{F10}")

        def _menu_opened():
            execute_step = self._menu_items_by_name("Execute Step")
            delete_action = self._menu_items_by_name("Delete Action")
            return execute_step and delete_action

        _retry("Action context menu", _menu_opened, timeout=8.0, interval=0.3)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="FleetAutomate smoke test using imported winauto_cli helpers.")
    parser.add_argument("--python-exe", type=Path, default=Path(_default_python()))
    parser.add_argument("--exe-path", type=Path, default=_default_exe())
    parser.add_argument("--recent-projects-file", type=Path, default=_default_recent_projects_file())
    parser.add_argument("--winauto-cli-repo", type=Path, default=_default_winauto_cli_repo())
    parser.add_argument("--launch-wait-seconds", type=float, default=4.0)
    parser.add_argument("--keep-open", action="store_true", help="Keep FleetAutomate open after the smoke test finishes.")
    return parser


def main() -> int:
    args = build_parser().parse_args()

    recent_project = _read_recent_project(args.recent_projects_file)
    project_path = Path(recent_project["FilePath"])
    project_name = str(recent_project["ProjectName"])
    first_flow_name, first_flow_path, expected_root_action_count = _read_project_metadata(project_path)

    tester = SmokeTester(
        python_exe=args.python_exe,
        exe_path=args.exe_path,
        recent_projects_file=args.recent_projects_file,
        winauto_cli_repo=args.winauto_cli_repo,
        launch_wait_seconds=args.launch_wait_seconds,
    )

    tester.log(f"Most recent project: {project_name}")
    tester.log(f"First TestFlow file: {first_flow_path}")
    tester.log(f"Expected first TestFlow root action count: {expected_root_action_count}")

    try:
        tester.launch()
        tester.open_most_recent_project(project_name)
        tester.wait_for_testflows_loaded(expected_min_count=1)
        tester.assert_no_testflow_load_errors()
        tester.open_first_testflow(first_flow_name)
        tester.assert_action_tree_count(expected_root_action_count)
        tester.assert_action_context_menu()
        tester.log("Smoke test passed")
        return 0
    finally:
        if not args.keep_open:
            tester.cleanup()


if __name__ == "__main__":
    raise SystemExit(main())
