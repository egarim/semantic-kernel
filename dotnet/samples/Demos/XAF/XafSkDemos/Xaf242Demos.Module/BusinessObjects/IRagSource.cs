// Copyright (c) Microsoft. All rights reserved.
#pragma warning disable IDE0003
#pragma warning disable IDE0009
#pragma warning disable IDE0040

using System;
using System.Linq;


namespace Xaf242Demos.Module.BusinessObjects;

public interface IRagSource
{
    string GetCollectionName();
    IEnumerable<string> GetRecordCollection();

    string GetOwnerKey();
}
