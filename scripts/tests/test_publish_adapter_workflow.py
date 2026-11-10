import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW_PATH = REPOSITORY_ROOT / ".github" / "workflows" / "publish-adapter-pages.yml"


class PublishAdapterWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

    def test_release_zip_is_verified_and_never_signed_in_place(self):
        self.assertNotIn("sign-package", self.workflow)
        self.assertIn("gh release view \"$TAG\" --json assets", self.workflow)
        self.assertIn("sha256sum", self.workflow)
        self.assertIn("release validate", self.workflow)
        self.assertIn(
            'test "$pages_sha256" = "${{ steps.package.outputs.sha256 }}"',
            self.workflow,
        )
        self.assertIn('test ! -e "$public_package"', self.workflow)

    def test_existing_release_can_be_reprocessed_without_moving_its_tag(self):
        self.assertIn("workflow_dispatch:", self.workflow)
        self.assertIn("tag:", self.workflow)
        self.assertIn("github.event.release.tag_name || github.event.inputs.tag", self.workflow)
        self.assertIn("test ! -e \"$descriptor\"", self.workflow)

    def test_manual_recovery_does_not_announce_unless_requested(self):
        self.assertIn("announce:", self.workflow)
        self.assertIn("github.event.inputs.announce == 'true'", self.workflow)


if __name__ == "__main__":
    unittest.main()
