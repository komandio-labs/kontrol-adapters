using Kontrol.Sdk.Inputs;
using Kontrol.Sdk.IPC;

namespace Kontrol.Sdk.Interfaces;

public interface IKontrolGameAdapter
{
    string AdapterId { get; }
    AdapterInputSchema InputSchema { get; }
    void OnInputFrame(in InputFrame inputFrame);
}
