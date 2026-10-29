namespace Lokad.Utf8Regex.PythonRe;

public sealed partial class Utf8PythonRegex : IDisposable
{
    private PythonReProgramMemory? _programMemory;

    private PythonReProgramMemory.Use? EnterProgramUse() => _programMemory?.Enter();

    /// <summary>Closes caller-funded compilation storage after the last active operation or cursor finishes.</summary>
    /// <remarks>New matching uses of a funded expression fail after disposal. In-flight uses retain their funding and complete under their original controls; disposal neither waits nor starts background work. Existing snapshots remain valid. Disposal is a no-op for expressions compiled without a caller-owned program allowance.</remarks>
    public void Dispose() => _programMemory?.Close();
}
