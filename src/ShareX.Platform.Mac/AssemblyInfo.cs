using System.Runtime.CompilerServices;

// Lets ShareX.Platform.Mac.Tests exercise internal, dylib-free pieces (JSON
// request shaping, result-code mapping) without needing InternalsVisibleTo
// support in MSBuild item form.
[assembly: InternalsVisibleTo("ShareX.Platform.Mac.Tests")]
