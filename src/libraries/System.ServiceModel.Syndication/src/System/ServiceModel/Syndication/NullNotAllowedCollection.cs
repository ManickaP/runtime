// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;

namespace System.ServiceModel.Syndication
{
    internal sealed class NullNotAllowedCollection<TCollectionItem> : Collection<TCollectionItem> where TCollectionItem : class
    {
        public NullNotAllowedCollection() : base()
        {
        }

        protected override void InsertItem(int index, TCollectionItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, TCollectionItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            base.SetItem(index, item);
        }
    }
}
