// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;

namespace System.Reflection
{
    internal sealed class SignatureConstructedGenericType : SignatureType
    {
        // The exception-visible name "typeArguments" is chosen to match the parameter name to Type.MakeGenericType() since that's the
        // intended user of this constructor.
        internal SignatureConstructedGenericType(Type genericTypeDefinition, Type[] typeArguments)
        {
            Debug.Assert(genericTypeDefinition != null);
            Debug.Assert(typeArguments != null);
            Debug.Assert(genericTypeDefinition.IsGenericTypeDefinition);

            typeArguments = (Type[])(typeArguments.Clone());
            for (int i = 0; i < typeArguments.Length; i++)
            {
                ArgumentNullException.ThrowIfNull(typeArguments[i], nameof(typeArguments));
            }

            _genericTypeDefinition = genericTypeDefinition;
            _genericTypeArguments = typeArguments;
        }

        public sealed override bool IsTypeDefinition => false;
        public sealed override bool IsGenericTypeDefinition => false;
        protected sealed override bool HasElementTypeImpl() => false;
        protected sealed override bool IsArrayImpl() => false;
        protected sealed override bool IsByRefImpl() => false;
        public sealed override bool IsByRefLike => _genericTypeDefinition.IsByRefLike;
        public sealed override bool IsEnum => _genericTypeDefinition.IsEnum;
        protected sealed override bool IsPointerImpl() => false;
        public sealed override bool IsSZArray => false;
        public sealed override bool IsVariableBoundArray => false;
        public sealed override bool IsConstructedGenericType => true;
        public sealed override bool IsGenericParameter => false;
        public sealed override bool IsGenericTypeParameter => false;
        public sealed override bool IsGenericMethodParameter => false;
        public sealed override bool ContainsGenericParameters
        {
            get
            {
                for (int i = 0; i < _genericTypeArguments.Length; i++)
                {
                    if (_genericTypeArguments[i].ContainsGenericParameters)
                        return true;
                }
                return false;
            }
        }

        protected sealed override bool IsValueTypeImpl() => _genericTypeDefinition.IsValueType;
        internal sealed override SignatureType? ElementType => null;
        public sealed override int GetArrayRank() => throw new ArgumentException(SR.Argument_HasToBeArrayClass);
        public sealed override Type GetGenericTypeDefinition() => _genericTypeDefinition;
        public sealed override Type[] GetGenericArguments() => GenericTypeArguments;
        public sealed override Type[] GenericTypeArguments => (Type[])(_genericTypeArguments.Clone());
        public sealed override int GenericParameterPosition => throw new InvalidOperationException(SR.Arg_NotGenericParameter);
        public sealed override string Name => _genericTypeDefinition.Name;
        public sealed override string? Namespace => _genericTypeDefinition.Namespace;

        public sealed override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_genericTypeDefinition.ToString());
            sb.Append('[');
            for (int i = 0; i < _genericTypeArguments.Length; i++)
            {
                if (i != 0)
                    sb.Append(',');
                sb.Append(_genericTypeArguments[i].ToString());
            }
            sb.Append(']');
            return sb.ToString();
        }

        private readonly Type _genericTypeDefinition;
        private readonly Type[] _genericTypeArguments;
    }
}
