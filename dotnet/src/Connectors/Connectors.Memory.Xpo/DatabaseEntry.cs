// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Connectors.Xpo;
internal struct DatabaseEntry
{
    public string Key { get; set; }

    public string MetadataString { get; set; }

    public string EmbeddingString { get; set; }

    public string? Timestamp { get; set; }
}
