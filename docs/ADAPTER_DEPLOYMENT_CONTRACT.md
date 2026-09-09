# Adapter-owned deployment contract

`IAdapterInstaller` retains its existing execution members and adds the
contextual `GetDeploymentPlan(AdapterDeploymentContext)` member. It has a
default implementation, so an adapter compiled against an older SDK continues
to load and receives conservative generic deployment data when used by a newer
host.

This compatibility is intentionally one-way: a host using the new SDK supports
adapters compiled against SDK 1.3 and earlier. An adapter that implements the
new deployment schema requires a host carrying the new SDK.

The legacy fallback never infers deployment locations, file effects, or launch
behavior from `GameLaunchMethod`; it explicitly reports that those details are
unavailable from the earlier adapter contract.

The plan is descriptive. `Install`, `Uninstall`, `CheckIsInstalled`, `Launch`,
and `CreateShortcut` remain the execution API. `GameLaunchMethod` remains a
coarse technical category; adapter-owned plan data is the source of
user-facing deployment and launch details when an adapter provides it.

For a current, resolved view of the selected target, a host calls
`GetDeploymentPlan(AdapterDeploymentContext)`. The context includes the chosen
game directory and packaged adapter entry path. New adapters should implement
this overload whenever locations, prerequisites, verification, or launch facts
depend on the current machine or selected game. Older adapters use the generic
fallback. Hosts must evaluate it from an I/O-owning service rather than a UI
property getter.

## External-loader example

An adapter that deploys a .NET Framework payload through an external loader can
describe the complete flow without adding loader-specific logic to Kontrol:

```csharp
using System;
using Kontrol.Sdk.Attributes;
using Kontrol.Sdk.Interfaces;

public sealed class ExampleInstaller : IAdapterInstaller
{
    public AdapterDeploymentPlan GetDeploymentPlan(AdapterDeploymentContext context) =>
        context.Method == GameLaunchMethod.BinPluginsFolder
            ? new AdapterDeploymentPlan(
                context.Method,
                new DeploymentMethodCapabilities(true, true, true, false),
                Title: "Example Legacy loader",
                Summary: "Kontrol copies the adapter payload to the loader-managed local plugin directory and starts the loader.",
                Effects: "Only the adapter payload is copied to the resolved loader directory; game files and Steam settings are unchanged.",
                Consent: "Confirm that the external loader will be started and its local plugin directory will be modified.",
                Prerequisites:
                [
                    new DeploymentPrerequisite(
                        "legacy-loader",
                        "Legacy loader",
                        "The external loader executable must be installed.",
                        DeploymentPrerequisiteState.Satisfied,
                        "C:\\Program Files\\ExampleLoader",
                        "Install the Legacy loader, then retry discovery.")
                ],
                Targets:
                [
                    new DeploymentTarget(
                        "loader-local",
                        "Loader local plugins",
                        DeploymentTargetKind.ExternalLoader,
                        "C:\\Program Files\\ExampleLoader\\Legacy\\Local",
                        [new DeploymentOwnedFile(
                            "Example.Adapter.Plugin.dll",
                            "The adapter payload copied to the loader-local directory.")],
                        [new DeploymentConfigurationEffect(
                            "loader-profile",
                            "The adapter plugin is enabled in the selected loader profile.")])
                ],
                LaunchChain: new DeploymentLaunchChain(
                [
                    new DeploymentLaunchStep(
                        "External loader",
                        DeploymentLaunchStepKind.ExternalLauncher,
                        "Starts the loader with the selected game executable.",
                        "C:\\Program Files\\ExampleLoader\\Legacy.exe",
                        "\"{gameExecutable}\"")
                ]),
                ManualSteps: [new DeploymentManualStep("Enable the adapter plugin in the external loader profile.")],
                Verification: new DeploymentVerification(
                    DeploymentState.Deployed,
                    "The payload is present in the loader's local plugin directory.",
                    DeploymentRuntimeState.Ready,
                    "The adapter reports a ready heartbeat after the loader starts."),
                Rollback: new DeploymentRollbackPlan(
                    "Remove the adapter-owned payload from the loader's local plugin directory.",
                    [new DeploymentRollbackEffect(
                        "loader-local",
                        "Delete Example.Adapter.Plugin.dll from the loader-local target.")]))
            : throw new NotSupportedException($"Unsupported deployment method: {context.Method}");

    public void Install(string gameDirectory, GameLaunchMethod method, string sourceDllPath) =>
        throw new NotImplementedException();

    public void Uninstall(string gameDirectory, GameLaunchMethod method) =>
        throw new NotImplementedException();

    public bool CheckIsInstalled(string gameDirectory, GameLaunchMethod method) => false;

    public void Launch(string gameDirectory, GameLaunchMethod method, string sourceDllPath) =>
        throw new NotImplementedException();

    public void CreateShortcut(string gameDirectory, GameLaunchMethod method, string sourceDllPath) =>
        throw new NotSupportedException();
}
```

Paths in a real plan should be resolved by the adapter's discovery logic and
must be accompanied by an actionable prerequisite/remediation result. A plan
must describe only files and configuration owned by the adapter; it must not
claim ownership of the game installation or Steam settings when deployment is
performed by an external loader.

## Compatibility rules

- Do not remove or change existing `IAdapterInstaller` members.
- `GetDeploymentInformation`, `GetCapabilities`, and the non-contextual
  `GetDeploymentPlan(GameLaunchMethod)` are obsolete. Implement the contextual
  `GetDeploymentPlan(AdapterDeploymentContext)` and set its `Capabilities`
  instead. `DeploymentMethodInformation` is also obsolete and remains only for
  the old-adapter fallback.
- New members must have default implementations or be additive types.
- Older adapters use `AdapterDeploymentPlan.Generic(...)`, which preserves the
  older adapter's `GetDeploymentInformation` and `GetCapabilities` overrides.
- New adapters should return explicit capabilities, targets, owned files,
  configuration effects, launch steps, manual steps, verification states, and
  rollback effects for every supported method.
