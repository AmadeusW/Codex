using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;

namespace Perspectil
{
    public class PerspectiveAssemblyGenerator
    {
        private static HashSet<string> s_restrictedAssemblyNames = new HashSet<string>(new[] { "mscorlib", "System.Runtime" }, StringComparer.OrdinalIgnoreCase);

        private AssemblyDefinition originAssembly;
        private MemoryStream assemblyStream = new MemoryStream();

        public readonly List<AssemblyDefinition> PerspectiveAssemblies = new List<AssemblyDefinition>();
        public readonly ConcurrentDictionary<TypeReference, TypeDefinition> TypeDefinitionMap = new ConcurrentDictionary<TypeReference, TypeDefinition>();
        public readonly ConcurrentDictionary<MemberReference, IMemberDefinition> MemberDefinitionMap = new ConcurrentDictionary<MemberReference, IMemberDefinition>();

        public PerspectiveAssemblyGenerator(AssemblyDefinition assembly)
        {
            this.originAssembly = assembly;
            originAssembly.Write(assemblyStream);
        }

        public PerspectiveAssemblyGenerator(AssemblyDefinition assembly, Stream assemblyStream)
        {
            this.originAssembly = assembly;
            assemblyStream.Position = 0;
            assemblyStream.CopyTo(this.assemblyStream);
        }

        public static PerspectiveAssemblyGenerator CreateForFile(string filePath)
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                return new PerspectiveAssemblyGenerator(AssemblyDefinition.ReadAssembly(stream), stream);
            }
        }

        public void Generate()
        {
            var memberReferences = originAssembly.MainModule.Assembly.MainModule.GetMemberReferences().Concat(originAssembly.MainModule.GetTypeReferences()).Reverse();
            var memberReferenceLookup = memberReferences.ToLookup(memref => GetScope(memref));
            foreach (var scopedKeyMemberReferences in memberReferenceLookup)
            {
                if (scopedKeyMemberReferences?.Key == null)
                {
                    continue;
                }

                switch (scopedKeyMemberReferences.Key.MetadataScopeType)
                {
                    case MetadataScopeType.AssemblyNameReference:
                        CreateDefinition((AssemblyNameReference)scopedKeyMemberReferences.Key, scopedKeyMemberReferences);
                        break;
                    case MetadataScopeType.ModuleReference:
                        break;
                    case MetadataScopeType.ModuleDefinition:
                        break;
                    default:
                        break;
                }
            }
        }

        private static IMetadataScope GetScope(MemberReference memberReference)
        {
            if (memberReference.DeclaringType == null)
            {
                return ((TypeReference)memberReference).Scope;
            }

            return memberReference.DeclaringType.Scope;
        }

        public void CreateDefinition(AssemblyNameReference assemblyNameReference, IEnumerable<MemberReference> memberReferences)
        {
            if (s_restrictedAssemblyNames.Contains(assemblyNameReference.Name))
            {
                return;
            }

            var assemblyNameDefinition = new AssemblyNameDefinition(assemblyNameReference.Name, assemblyNameReference.Version);

            assemblyNameDefinition.PublicKey = assemblyNameReference.PublicKey;
            assemblyNameDefinition.PublicKeyToken = assemblyNameReference.PublicKeyToken;
            assemblyNameDefinition.Culture = assemblyNameReference.Culture;

            assemblyStream.Position = 0;
            var definition = AssemblyDefinition.ReadAssembly(assemblyStream, new ReaderParameters(ReadingMode.Immediate));
            var mainModule = definition.MainModule;
            var typesToRemove = new HashSet<TypeDefinition>(mainModule.Types);
            definition.Name = assemblyNameDefinition;

            //memberReferences = mainModule.Assembly.MainModule.GetMemberReferences().Concat(mainModule.GetTypeReferences()).Reverse();
            memberReferences = mainModule.GetTypeReferences().Reverse();

            memberReferences = memberReferences
                .Where(mr => IsMember(assemblyNameReference, mr))
                .ToList();

            foreach (var memberReference in memberReferences)
            {
                switch (memberReference.MetadataToken.TokenType)
                {
                    case TokenType.TypeRef:
                    case TokenType.TypeDef:
                        TypeReference typeReference = (TypeReference)memberReference;
                        CreateTypeDefinition(mainModule, typeReference);
                        break;
                    default:
                        TypeDefinition declaringTypeDefinition = CreateTypeDefinition(mainModule, memberReference.DeclaringType);

                        if (memberReference is FieldReference)
                        {
                            CreateFieldDefinition((FieldReference)memberReference, declaringTypeDefinition);
                        }
                        else if (memberReference is MethodReference)
                        {
                            CreateMethodDefinition((MethodReference)memberReference, declaringTypeDefinition);
                        }
                        else if (memberReference is EventReference)
                        {
                            CreateEventDefinition((EventReference)memberReference, declaringTypeDefinition);
                        }
                        else if (memberReference is PropertyReference)
                        {
                            CreatePropertyDefinition((PropertyReference)memberReference, declaringTypeDefinition);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                }
            }

            foreach (var type in typesToRemove)
            {
                mainModule.Types.Remove(type);
            }

            definition.CustomAttributes.Clear();
            definition.MainModule.Attributes |= ModuleAttributes.ILOnly;
            PerspectiveAssemblies.Add(definition);
        }

        private bool IsMember(AssemblyNameReference assemblyNameReference, MemberReference mr)
        {
            var scope = GetScope(mr);
            if (scope.MetadataScopeType != MetadataScopeType.AssemblyNameReference)
            {
                return false;
            }

            AssemblyNameReference assemblyName = (AssemblyNameReference)scope;
            return assemblyName.FullName == assemblyNameReference.FullName;
        }

        private TDefinition GetOrCreateMember<TReference, TDefinition>(
            TReference reference,
            TypeDefinition declaringTypeDefinition,
            Func<TReference, TypeDefinition, bool, TDefinition> definitionFactory = null)
            where TReference : MemberReference
            where TDefinition : IMemberDefinition
        {
            IMemberDefinition definition;
            if (MemberDefinitionMap.TryGetValue(reference, out definition))
            {
                return (TDefinition)definition;
            }

            var memberDefinition = definitionFactory(reference, declaringTypeDefinition, false);
            MemberDefinitionMap[reference] = memberDefinition;
            return memberDefinition;
        }

        private TypeReference ImportType(TypeReference typeReference, TypeDefinition declaringTypeDefinition, IGenericParameterProvider context)
        {
            TypeDefinition definition;
            if (TypeDefinitionMap.TryGetValue(typeReference, out definition))
            {
                return definition;
            }

            return typeReference;

            if (typeReference.IsGenericInstance)
            {
                GenericInstanceType genericInstanceTypeReference = (GenericInstanceType)typeReference;
                GenericInstanceType genericInstanceType = new GenericInstanceType(new TypeReference(typeReference.Namespace, typeReference.Name, declaringTypeDefinition.Module, declaringTypeDefinition.Module, typeReference.IsValueType));
                foreach (var genericArgument in genericInstanceTypeReference.GenericArguments)
                {
                    genericInstanceType.GenericArguments.Add(ImportType(genericArgument, declaringTypeDefinition, context));
                    //reference.GenericParameters.Add()
                }

                return genericInstanceType;
            }

            if (typeReference.IsGenericParameter)
            {
                GenericParameter genericParameterSource = (GenericParameter)typeReference;
                GenericParameter genericParameterTarget = new GenericParameter(genericParameterSource.Name, context);
                return genericParameterTarget;
            }

            return declaringTypeDefinition.Module.ImportReference(typeReference, context);

        }

        private MethodDefinition CreateMethodDefinition(MethodReference reference, TypeDefinition declaringTypeDefinition, bool recursionGuard = true)
        {
            if (MemberDefinitionMap.ContainsKey(reference) || recursionGuard)
            {
                return GetOrCreateMember(reference, declaringTypeDefinition, CreateMethodDefinition);
            }

            MethodDefinition definition = new MethodDefinition(reference.Name, MethodAttributes.Public, reference.ReturnType);
            definition.ReturnType = ImportType(reference.ReturnType, declaringTypeDefinition, definition);

            foreach (var parameter in reference.Parameters)
            {
                definition.Parameters.Add(CreateParameterDefinition(parameter, declaringTypeDefinition, definition));
            }

            foreach (var genericParameter in reference.GenericParameters)
            {
                definition.GenericParameters.Add((GenericParameter)ImportType(genericParameter, declaringTypeDefinition, definition));
            }

            declaringTypeDefinition.Methods.Add(definition);
            return definition;
        }

        private ParameterDefinition CreateParameterDefinition(ParameterDefinition parameter, TypeDefinition declaringTypeDefinition, IGenericParameterProvider context)
        {
            return new ParameterDefinition(parameter.Name, parameter.Attributes, ImportType(parameter.ParameterType, declaringTypeDefinition, context));
        }

        private PropertyDefinition CreatePropertyDefinition(PropertyReference reference, TypeDefinition declaringTypeDefinition, bool recursionGuard = true)
        {
            if (MemberDefinitionMap.ContainsKey(reference) || recursionGuard)
            {
                return GetOrCreateMember(reference, declaringTypeDefinition, CreatePropertyDefinition);
            }

            PropertyDefinition definition = new PropertyDefinition(reference.Name, PropertyAttributes.None, ImportType(reference.PropertyType, declaringTypeDefinition, context: null));
            foreach (var parameter in reference.Parameters)
            {
                definition.Parameters.Add(CreateParameterDefinition(parameter, declaringTypeDefinition, context: null));
            }

            declaringTypeDefinition.Properties.Add(definition);
            return definition;
        }

        private EventDefinition CreateEventDefinition(EventReference reference, TypeDefinition declaringTypeDefinition, bool recursionGuard = true)
        {
            if (MemberDefinitionMap.ContainsKey(reference) || recursionGuard)
            {
                return GetOrCreateMember(reference, declaringTypeDefinition, CreateEventDefinition);
            }

            EventDefinition definition = new EventDefinition(reference.Name, EventAttributes.None, ImportType(reference.EventType, declaringTypeDefinition, context: null));
            declaringTypeDefinition.Events.Add(definition);
            return definition;
        }

        private FieldDefinition CreateFieldDefinition(FieldReference reference, TypeDefinition declaringTypeDefinition, bool recursionGuard = true)
        {
            if (MemberDefinitionMap.ContainsKey(reference) || recursionGuard)
            {
                return GetOrCreateMember(reference, declaringTypeDefinition, CreateFieldDefinition);
            }

            FieldDefinition definition = new FieldDefinition(reference.Name, FieldAttributes.Public, ImportType(reference.FieldType, declaringTypeDefinition, context: null));
            declaringTypeDefinition.Fields.Add(definition);
            return definition;
        }

        private TypeDefinition CreateTypeDefinition(ModuleDefinition mainModule, TypeReference reference)
        {
            TypeDefinition definition;
            if (TypeDefinitionMap.TryGetValue(reference, out definition))
            {
                return definition;
            }

            definition = new TypeDefinition(reference.Namespace, reference.Name, TypeAttributes.Public);
            foreach (var genericParameter in reference.GenericParameters)
            {
                definition.GenericParameters.Add((GenericParameter)ImportType(genericParameter, definition, definition));
            }

            TypeDefinitionMap[reference] = definition;

            if (reference.DeclaringType != null)
            {
                TypeDefinition declaringTypeDefinition = CreateTypeDefinition(mainModule, reference.DeclaringType);
                declaringTypeDefinition.NestedTypes.Add(definition);
            }
            else
            {
                mainModule.Types.Add(definition);
            }

            return definition;
        }
    }
}
