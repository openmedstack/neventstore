﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace OpenMedStack.NEventStore {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Messages {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Messages() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("OpenMedStack.NEventStore.Messages", typeof(Messages).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Adding wireup registration callback..
        /// </summary>
        internal static string AddingWireupCallback {
            get {
                return ResourceManager.GetString("AddingWireupCallback", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Adding wireup registration for an object instance of type &apos;{0}&apos;..
        /// </summary>
        internal static string AddingWireupRegistration {
            get {
                return ResourceManager.GetString("AddingWireupRegistration", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Attempting to resolve existing instance..
        /// </summary>
        internal static string AttemptingToResolveInstance {
            get {
                return ResourceManager.GetString("AttemptingToResolveInstance", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Configuring SQL engine to auto-detect dialect..
        /// </summary>
        internal static string AutoDetectDialect {
            get {
                return ResourceManager.GetString("AutoDetectDialect", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Building (and storing) new instance for later calls..
        /// </summary>
        internal static string BuildingAndStoringNewInstance {
            get {
                return ResourceManager.GetString("BuildingAndStoringNewInstance", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Building the persistence engine..
        /// </summary>
        internal static string BuildingEngine {
            get {
                return ResourceManager.GetString("BuildingEngine", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Building new instance..
        /// </summary>
        internal static string BuildingNewInstance {
            get {
                return ResourceManager.GetString("BuildingNewInstance", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Head CommitSequence [{0}] greater or equal than Attempt CommitSequence [{1}] - StreamId {2} - StreamRevision {3} - Events Count {4}.
        /// </summary>
        internal static string ConcurrencyExceptionCommitSequence {
            get {
                return ResourceManager.GetString("ConcurrencyExceptionCommitSequence", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Head StreamRevision [{0}] greater or equal than Attempt StreamRevision [{1}] - StreamId {2} - StreamRevision {3} - Events Count {4}.
        /// </summary>
        internal static string ConcurrencyExceptionStreamRevision {
            get {
                return ResourceManager.GetString("ConcurrencyExceptionStreamRevision", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Configuring serializer to compress the serialized payload..
        /// </summary>
        internal static string ConfiguringCompression {
            get {
                return ResourceManager.GetString("ConfiguringCompression", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Configuring serializer to encrypt the serialized payload..
        /// </summary>
        internal static string ConfiguringEncryption {
            get {
                return ResourceManager.GetString("ConfiguringEncryption", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Configuring persistence engine to enlist in ambient transactions using TransactionScope..
        /// </summary>
        internal static string ConfiguringEngineEnlistment {
            get {
                return ResourceManager.GetString("ConfiguringEngineEnlistment", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Configuring persistence engine to initialize..
        /// </summary>
        internal static string ConfiguringEngineInitialization {
            get {
                return ResourceManager.GetString("ConfiguringEngineInitialization", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Configuring persistence engine to track performance.
        /// </summary>
        internal static string ConfiguringEnginePerformanceTracking {
            get {
                return ResourceManager.GetString("ConfiguringEnginePerformanceTracking", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registration configured to resolve a new instance per call..
        /// </summary>
        internal static string ConfiguringInstancePerCall {
            get {
                return ResourceManager.GetString("ConfiguringInstancePerCall", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Using SQL connection factory of type &apos;{0}&apos;..
        /// </summary>
        internal static string ConnectionFactorySpecified {
            get {
                return ResourceManager.GetString("ConnectionFactorySpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Deserializing stream.
        /// </summary>
        internal static string DeserializingStream {
            get {
                return ResourceManager.GetString("DeserializingStream", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registering SQL dialect of type &apos;{0}&apos;..
        /// </summary>
        internal static string DialectSpecified {
            get {
                return ResourceManager.GetString("DialectSpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Duplicate commit id {0}..
        /// </summary>
        internal static string DuplicateCommitIdException {
            get {
                return ResourceManager.GetString("DuplicateCommitIdException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Configuring the store to upconvert events when fetched..
        /// </summary>
        internal static string EventUpconverterRegistered {
            get {
                return ResourceManager.GetString("EventUpconverterRegistered", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Will scan for event upconverters from the following assemblies: &apos;{0}&apos;.
        /// </summary>
        internal static string EventUpconvertersLoadedFrom {
            get {
                return ResourceManager.GetString("EventUpconvertersLoadedFrom", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot only compare {0} with {1}..
        /// </summary>
        internal static string FailedToCompareCheckpoint {
            get {
                return ResourceManager.GetString("FailedToCompareCheckpoint", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Initializing the configured persistence engine..
        /// </summary>
        internal static string InitializingEngine {
            get {
                return ResourceManager.GetString("InitializingEngine", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Inspecting text stream.
        /// </summary>
        internal static string InspectingTextStream {
            get {
                return ResourceManager.GetString("InspectingTextStream", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The instance provided cannot be null..
        /// </summary>
        internal static string InstanceCannotBeNull {
            get {
                return ResourceManager.GetString("InstanceCannotBeNull", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid key length.
        /// </summary>
        internal static string InvalidKeyLength {
            get {
                return ResourceManager.GetString("InvalidKeyLength", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Persistence engine configured to page every &apos;{0}&apos; records..
        /// </summary>
        internal static string PagingSpecified {
            get {
                return ResourceManager.GetString("PagingSpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registering persistence engine of type &apos;{0}&apos;..
        /// </summary>
        internal static string RegisteringPersistenceEngine {
            get {
                return ResourceManager.GetString("RegisteringPersistenceEngine", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registering wireup instance for service of type &apos;{0}&apos;..
        /// </summary>
        internal static string RegisteringServiceInstance {
            get {
                return ResourceManager.GetString("RegisteringServiceInstance", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registering wireup resolver for service of type &apos;{0}&apos;..
        /// </summary>
        internal static string RegisteringWireupCallback {
            get {
                return ResourceManager.GetString("RegisteringWireupCallback", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Resolving instance..
        /// </summary>
        internal static string ResolvingInstance {
            get {
                return ResourceManager.GetString("ResolvingInstance", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Attempting to resolve instance for service of type &apos;{0}&apos;..
        /// </summary>
        internal static string ResolvingService {
            get {
                return ResourceManager.GetString("ResolvingService", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Serializing graph.
        /// </summary>
        internal static string SerializingGraph {
            get {
                return ResourceManager.GetString("SerializingGraph", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Head CommitSequence [{0}] lesser than Attempt CommitSequence - 1 [{1}] - StreamId {2} - StreamRevision {3} - Events Count {4}.
        /// </summary>
        internal static string StorageExceptionCommitSequence {
            get {
                return ResourceManager.GetString("StorageExceptionCommitSequence", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Stream EOF: head.StreamRevision [{0}] &lt; attempt.StreamRevision [{1}] - attempt.Events.Count [{2}] - StreamId {3} - StreamRevision {4} .
        /// </summary>
        internal static string StorageExceptionEndOfStream {
            get {
                return ResourceManager.GetString("StorageExceptionEndOfStream", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Registering stream ID hasher of type &apos;{0}&apos;.
        /// </summary>
        internal static string StreamIdHasherSpecified {
            get {
                return ResourceManager.GetString("StreamIdHasherSpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Stream not found: StreamId {0} bucket {1}..
        /// </summary>
        internal static string StreamNotFoundException {
            get {
                return ResourceManager.GetString("StreamNotFoundException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The type provided must be registered as an interface rather than as a concrete type, e.g. &quot;container.Register&lt;ISomeService&gt;(instance);&quot;..
        /// </summary>
        internal static string TypeMustBeInterface {
            get {
                return ResourceManager.GetString("TypeMustBeInterface", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to resolve requested instance of type &apos;{0}&apos;..
        /// </summary>
        internal static string UnableToResolve {
            get {
                return ResourceManager.GetString("UnableToResolve", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Wrapping serializer of type &apos;{0}&apos; in RijndaelSerializer..
        /// </summary>
        internal static string WrappingSerializerEncryption {
            get {
                return ResourceManager.GetString("WrappingSerializerEncryption", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Wrapping serializer of type &apos;{0}&apos; in GZipSerializer..
        /// </summary>
        internal static string WrappingSerializerGZip {
            get {
                return ResourceManager.GetString("WrappingSerializerGZip", resourceCulture);
            }
        }
    }
}
