// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Xpo;

namespace Microsoft.SemanticKernel.Connectors.Xpo;

internal sealed class XpoDatabase
{
    public XpoDatabase() { }

    public Task CreateTableAsync(IDataLayer conn, CancellationToken cancellationToken = default)
    {
        IDataLayer dl = conn;
        using (Session session = new(dl))
        {
            System.Reflection.Assembly[] assemblies = new System.Reflection.Assembly[] {
                    typeof(XpoDatabaseEntry).Assembly };
            session.UpdateSchema(assemblies);
            session.CreateObjectTypeRecords(assemblies);
        }
        return Task.CompletedTask;
    }

    public async Task CreateCollectionAsync(IDataLayer conn, string collectionName, CancellationToken cancellationToken = default)
    {
        if (await this.DoesCollectionExistsAsync(conn, collectionName, cancellationToken).ConfigureAwait(false))
        {
            // Collection already exists
            return;
        }

        UnitOfWork unitOfWork = new(conn);
        XpoDatabaseEntry entry = new(unitOfWork)
        {
            Collection = collectionName
        };
        await unitOfWork.CommitChangesAsync(cancellationToken).ConfigureAwait(false);


    }

    public async Task UpdateAsync(IDataLayer conn,
        string collection, string key, string? metadata, string? embedding, string? timestamp, CancellationToken cancellationToken = default)
    {
        UnitOfWork unitOfWork = new(conn);
        var entry = unitOfWork.Query<XpoDatabaseEntry>().FirstOrDefault(x => x.Collection == collection && x.Key == key);
        if (entry != null)
        {
            entry.MetadataString = metadata ?? string.Empty;
            entry.EmbeddingString = embedding ?? string.Empty;
            entry.Timestamp = timestamp ?? string.Empty;
            await unitOfWork.CommitChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task InsertOrIgnoreAsync(IDataLayer conn,
        string collection, string key, string? metadata, string? embedding, string? timestamp, CancellationToken cancellationToken = default)
    {
        UnitOfWork unitOfWork = new(conn);
        if (unitOfWork.Query<XpoDatabaseEntry>().FirstOrDefault(x => x.Collection == collection && x.Key == key) == null)
        {
            XpoDatabaseEntry entry = new(unitOfWork)
            {
                Collection = collection,
                Key = key,
                MetadataString = metadata ?? string.Empty,
                EmbeddingString = embedding ?? string.Empty,
                Timestamp = timestamp ?? string.Empty
            };
            await unitOfWork.CommitChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> DoesCollectionExistsAsync(IDataLayer conn,
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        var collections = await this.GetCollectionsAsync(conn, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
        return collections.Contains(collectionName);
    }

    public async IAsyncEnumerable<string> GetCollectionsAsync(IDataLayer conn,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UnitOfWork unitOfWork = new(conn);
        var Collections = unitOfWork.Query<XpoDatabaseEntry>().Select(x => x.Collection).Distinct().ToList();
        foreach (string collection in Collections)
        {
            yield return collection;
        }
    }

    public async IAsyncEnumerable<DatabaseEntry> ReadAllAsync(IDataLayer conn,
        string collectionName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UnitOfWork unitOfWork = new(conn);
        var entries = unitOfWork.Query<XpoDatabaseEntry>().Where(x => x.Collection == collectionName).ToList();
        foreach (XpoDatabaseEntry entry in entries)
        {
            yield return entry.ToDatabaseEntry();
        }
    }

    public async Task<DatabaseEntry?> ReadAsync(IDataLayer conn,
        string collectionName,
        string key,
        CancellationToken cancellationToken = default)
    {
        UnitOfWork unitOfWork = new(conn);
        var entry = unitOfWork.Query<XpoDatabaseEntry>().FirstOrDefault(x => x.Collection == collectionName && x.Key == key);
        if (entry != null)
        {
            return entry.ToDatabaseEntry();
        }
        return null;
    }

    public Task DeleteCollectionAsync(IDataLayer conn, string collectionName, CancellationToken cancellationToken = default)
    {
        UnitOfWork unitOfWork = new(conn);
        var entries = unitOfWork.Query<XpoDatabaseEntry>().Where(x => x.Collection == collectionName).ToList();
        foreach (XpoDatabaseEntry entry in entries)
        {
            unitOfWork.Delete(entry);
        }
        unitOfWork.CommitChanges();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(IDataLayer conn, string collectionName, string key, CancellationToken cancellationToken = default)
    {
        UnitOfWork unitOfWork = new(conn);
        var entry = unitOfWork.Query<XpoDatabaseEntry>().FirstOrDefault(x => x.Collection == collectionName && x.Key == key);
        if (entry != null)
        {
            unitOfWork.Delete(entry);
            unitOfWork.CommitChanges();
        }
        return Task.CompletedTask;
    }

    public Task DeleteEmptyAsync(IDataLayer conn, string collectionName, CancellationToken cancellationToken = default)
    {
        UnitOfWork unitOfWork = new(conn);
        var entries = unitOfWork.Query<XpoDatabaseEntry>().Where(x => x.Collection == collectionName && x.Key == null).ToList();

        if (entries != null)
        {
            unitOfWork.Delete(entries);
            return unitOfWork.CommitChangesAsync(cancellationToken);
        }
        return Task.CompletedTask;
    }
}
