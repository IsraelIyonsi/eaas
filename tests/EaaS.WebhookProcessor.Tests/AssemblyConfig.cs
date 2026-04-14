// The SnsSignatureVerifier cert cache is process-static; parallel test classes that seed /
// clear it would race. Serialize the test assembly to keep that fixture consistent.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
