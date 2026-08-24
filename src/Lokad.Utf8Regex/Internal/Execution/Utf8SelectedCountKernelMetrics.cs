namespace Lokad.Utf8Regex.Internal.Execution;

// These values come from untimed semantic replays. Keeping counters out of the
// production loops avoids perturbing the kernels that the benchmark times.
internal readonly record struct Utf8SelectedCountKernelMetrics(
    string Route,
    string CandidateDefinition,
    int? Candidates,
    int? FullVerifications,
    int Matches,
    bool IncludesUtf8Validation);
