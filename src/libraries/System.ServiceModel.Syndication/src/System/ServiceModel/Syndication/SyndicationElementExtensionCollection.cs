// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace System.ServiceModel.Syndication
{
    // sealed because the ctor results in a call to the virtual InsertItem method
    public sealed class SyndicationElementExtensionCollection : Collection<SyndicationElementExtension>
    {
        private XmlBuffer _buffer;
        private readonly bool _initialized;

        internal SyndicationElementExtensionCollection() : this((XmlBuffer)null)
        {
        }

        internal SyndicationElementExtensionCollection(XmlBuffer buffer) : base()
        {
            _buffer = buffer;
            if (_buffer != null)
            {
                PopulateElements();
            }
            _initialized = true;
        }

        internal SyndicationElementExtensionCollection(SyndicationElementExtensionCollection source) : base()
        {
            _buffer = source._buffer;
            for (int i = 0; i < source.Items.Count; ++i)
            {
                base.Add(source.Items[i]);
            }
            _initialized = true;
        }

        public void Add(object extension)
        {
            if (extension is SyndicationElementExtension)
            {
                base.Add((SyndicationElementExtension)extension);
            }
            else
            {
                Add(extension, (DataContractSerializer)null);
            }
        }

        public void Add(string outerName, string outerNamespace, object dataContractExtension)
        {
            Add(outerName, outerNamespace, dataContractExtension, null);
        }

        public void Add(object dataContractExtension, DataContractSerializer serializer)
        {
            Add(null, null, dataContractExtension, serializer);
        }

        public void Add(string outerName, string outerNamespace, object dataContractExtension, XmlObjectSerializer dataContractSerializer)
        {
            ArgumentNullException.ThrowIfNull(dataContractExtension);

            dataContractSerializer ??= new DataContractSerializer(dataContractExtension.GetType());
            base.Add(new SyndicationElementExtension(outerName, outerNamespace, dataContractExtension, dataContractSerializer));
        }

        public void Add(object xmlSerializerExtension, XmlSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(xmlSerializerExtension);

            serializer ??= new XmlSerializer(xmlSerializerExtension.GetType());
            base.Add(new SyndicationElementExtension(xmlSerializerExtension, serializer));
        }

        public void Add(XmlReader xmlReader)
        {
            ArgumentNullException.ThrowIfNull(xmlReader);

            base.Add(new SyndicationElementExtension(xmlReader));
        }

        public XmlReader GetReaderAtElementExtensions()
        {
            XmlBuffer extensionsBuffer = GetOrCreateBufferOverExtensions();
            XmlDictionaryReader reader = extensionsBuffer.GetReader(0);
            reader.ReadStartElement();
            return reader;
        }

        public Collection<TExtension> ReadElementExtensions<TExtension>(string extensionName, string extensionNamespace)
        {
            return ReadElementExtensions<TExtension>(extensionName, extensionNamespace, new DataContractSerializer(typeof(TExtension)));
        }

        public Collection<TExtension> ReadElementExtensions<TExtension>(string extensionName, string extensionNamespace, XmlObjectSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(serializer);

            return ReadExtensions<TExtension>(extensionName, extensionNamespace, serializer, null);
        }

        public Collection<TExtension> ReadElementExtensions<TExtension>(string extensionName, string extensionNamespace, XmlSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(serializer);

            return ReadExtensions<TExtension>(extensionName, extensionNamespace, null, serializer);
        }

        internal void WriteTo(XmlWriter writer, Func<string, string, bool> shouldSkipElement)
        {
            if (_buffer != null)
            {
                using (XmlDictionaryReader reader = _buffer.GetReader(0))
                {
                    reader.ReadStartElement();
                    while (reader.IsStartElement())
                    {
                        if (shouldSkipElement != null && shouldSkipElement(reader.LocalName, reader.NamespaceURI))
                        {
                            reader.Skip();
                            continue;
                        }

                        writer.WriteNode(reader, false);
                    }
                }
            }
            else
            {
                for (int i = 0; i < Items.Count; ++i)
                {
                    Items[i].WriteTo(writer);
                }
            }
        }

        protected override void ClearItems()
        {
            base.ClearItems();

            Debug.Assert(_initialized, "The constructor should never clear the collection.");
            _buffer = null;
        }

        protected override void InsertItem(int index, SyndicationElementExtension item)
        {
            ArgumentNullException.ThrowIfNull(item);

            base.InsertItem(index, item);
            // clear the cached buffer if the operation is happening outside the constructor
            if (_initialized)
            {
                _buffer = null;
            }
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);

            Debug.Assert(_initialized, "The constructor should never remove items from the collection.");
            _buffer = null;
        }

        protected override void SetItem(int index, SyndicationElementExtension item)
        {
            ArgumentNullException.ThrowIfNull(item);

            base.SetItem(index, item);
            Debug.Assert(_initialized, "The constructor should never set items in the collection.");
            _buffer = null;
        }

        private XmlBuffer GetOrCreateBufferOverExtensions()
        {
            if (_buffer != null)
            {
                return _buffer;
            }

            XmlBuffer newBuffer = new XmlBuffer(int.MaxValue);
            using (XmlDictionaryWriter writer = newBuffer.OpenSection(XmlDictionaryReaderQuotas.Max))
            {
                writer.WriteStartElement(Rss20Constants.ExtensionWrapperTag);
                for (int i = 0; i < Count; ++i)
                {
                    this[i].WriteTo(writer);
                }
                writer.WriteEndElement();
            }
            newBuffer.CloseSection();
            newBuffer.Close();
            _buffer = newBuffer;
            return newBuffer;
        }

        private void PopulateElements()
        {
            using (XmlDictionaryReader reader = _buffer.GetReader(0))
            {
                reader.ReadStartElement();
                int index = 0;
                while (reader.IsStartElement())
                {
                    base.Add(new SyndicationElementExtension(_buffer, index, reader.LocalName, reader.NamespaceURI));
                    reader.Skip();
                    ++index;
                }
            }
        }

        private Collection<TExtension> ReadExtensions<TExtension>(string extensionName, string extensionNamespace, XmlObjectSerializer dcSerializer, XmlSerializer xmlSerializer)
        {
            if (string.IsNullOrEmpty(extensionName))
            {
                throw new ArgumentException(SR.ExtensionNameNotSpecified);
            }

            Debug.Assert((dcSerializer == null) != (xmlSerializer == null), "exactly one serializer should be supplied");
            // normalize the null and empty namespace
            extensionNamespace ??= string.Empty;
            Collection<TExtension> results = new Collection<TExtension>();
            for (int i = 0; i < Count; ++i)
            {
                if (extensionName != this[i].OuterName || extensionNamespace != this[i].OuterNamespace)
                {
                    continue;
                }
                if (dcSerializer != null)
                {
                    results.Add(this[i].GetObject<TExtension>(dcSerializer));
                }
                else
                {
                    results.Add(this[i].GetObject<TExtension>(xmlSerializer));
                }
            }
            return results;
        }
    }
}
