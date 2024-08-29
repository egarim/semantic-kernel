// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Connectors.Xpo;

internal static class DatabaseEntryExtensions
{
    public static DatabaseEntry ToDatabaseEntry(this XpoDatabaseEntry xpoEntry)
    {
        return new DatabaseEntry
        {
            Key = xpoEntry.Key,
            MetadataString = xpoEntry.MetadataString,
            EmbeddingString = xpoEntry.EmbeddingString,
            Timestamp = xpoEntry.Timestamp
        };
    }
}
