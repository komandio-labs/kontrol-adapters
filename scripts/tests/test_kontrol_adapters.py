import unittest
import sys
from pathlib import Path
from unittest.mock import MagicMock, Mock, call, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import kontrol_adapters


class KontrolAdaptersDispatchTests(unittest.TestCase):
    def test_both_se1_spellings_run_the_full_se1_test_flow(self):
        for slug in ("spaceengineers", "space-engineers"):
            with self.subTest(slug=slug):
                reference = MagicMock()
                inspection = Mock()
                inspection.is_file.return_value = True
                checklist = Mock()
                checklist.exists.return_value = False
                reference.__truediv__.side_effect = {
                    "inspection.json": inspection,
                    "manual-checklist.md": checklist,
                }.__getitem__

                project = Path("adapter.csproj")
                tests = Path("adapter.Tests.csproj")
                with (
                    patch.object(kontrol_adapters, "sync_space_engineers", return_value=reference) as sync,
                    patch.object(kontrol_adapters, "adapter_paths", return_value=(Path("adapter"), project, tests)),
                    patch.object(kontrol_adapters, "tool") as tool,
                    patch.object(kontrol_adapters, "run") as run,
                ):
                    kontrol_adapters.test_adapter(slug, None, skip_sync=False)

                sync.assert_called_once_with(None)
                tool.assert_has_calls([
                    call("validate", "adapter", "--adapter", "space-engineers"),
                    call(
                        "validate", "compatibility", "--adapter", "space-engineers",
                        "--inspection", str(inspection),
                    ),
                ])
                run.assert_has_calls([
                    call("dotnet", "build", str(project), "-c", "Debug"),
                    call("dotnet", "test", str(tests), "-c", "Debug"),
                ])
                checklist.write_text.assert_called_once()

    def test_se1_package_aliases_pass_the_canonical_slug_to_test_flow(self):
        project = Path("adapter.csproj")
        adapter_root = Path("empty-adapter-root")
        with (
            patch.object(kontrol_adapters, "manifest", return_value={"adapterVersion": "1.0.0"}),
            patch.object(kontrol_adapters, "adapter_paths", return_value=(adapter_root, project, Path("tests.csproj"))),
            patch.object(kontrol_adapters, "test_adapter") as test_adapter,
            patch.object(kontrol_adapters, "run"),
            patch.object(kontrol_adapters, "tool"),
        ):
            for slug in ("spaceengineers", "space-engineers"):
                with self.subTest(slug=slug):
                    kontrol_adapters.package(
                        slug, "1.0.0", None, "package.zip", overwrite=True, configuration="Debug"
                    )

        self.assertEqual(
            test_adapter.call_args_list,
            [
                call("space-engineers", None, False),
                call("space-engineers", None, False),
            ],
        )

    def test_both_se2_spellings_use_the_se2_test_flow(self):
        for slug in ("spaceengineers2", "space-engineers-2"):
            with self.subTest(slug=slug), patch.object(kontrol_adapters, "test_se2") as test_se2:
                kontrol_adapters.test_adapter(slug, None, skip_sync=True)
            test_se2.assert_called_once_with(None, True)


if __name__ == "__main__":
    unittest.main()
