// Copyright (c) Microsoft. All rights reserved.

using System;
using DevExpress.Xpo;

namespace Microsoft.SemanticKernel.Connectors.Xpo;
public class XpoDatabaseEntry : XPLiteObject
{
    public XpoDatabaseEntry(Session session) : base(session)
    {
    }

    private string _oid;
    private string _collection;
    private string _timestamp;
    private string _embeddingString;
    private string _metadataString;
    private string _key;

    [Key(false)]
    [Size(SizeAttribute.DefaultStringMappingFieldSize)]
    public string Oid
    {
        get => this._oid;
        set => this.SetPropertyValue(nameof(this.Oid), ref this._oid, value);
    }

    [Size(SizeAttribute.DefaultStringMappingFieldSize)]
    public string Key
    {
        get => this._key;
        set => this.SetPropertyValue(nameof(this.Key), ref this._key, value);
    }

    [Size(SizeAttribute.Unlimited)]
    public string MetadataString
    {
        get => this._metadataString;
        set => this.SetPropertyValue(nameof(this.MetadataString), ref this._metadataString, value);
    }

    [Size(SizeAttribute.Unlimited)]
    public string EmbeddingString
    {
        get => this._embeddingString;
        set => this.SetPropertyValue(nameof(this.EmbeddingString), ref this._embeddingString, value);
    }

    [Size(SizeAttribute.DefaultStringMappingFieldSize)]
    public string Timestamp
    {
        get => this._timestamp;
        set => this.SetPropertyValue(nameof(this.Timestamp), ref this._timestamp, value);
    }

    [Size(SizeAttribute.DefaultStringMappingFieldSize)]
    public string Collection
    {
        get => this._collection;
        set => this.SetPropertyValue(nameof(this.Collection), ref this._collection, value);
    }
    protected override void OnSaving()
    {
        if (this.Session.IsNewObject(this))
        {
            this.Oid = Guid.NewGuid().ToString();
        }
        base.OnSaving();
    }
}
