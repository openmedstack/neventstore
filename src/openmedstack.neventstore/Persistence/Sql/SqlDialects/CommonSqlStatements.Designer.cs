﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace OpenMedStack.NEventStore.Persistence.Sql.SqlDialects {
    using System;
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class CommonSqlStatements {
        
        private static System.Resources.ResourceManager resourceMan;
        
        private static System.Globalization.CultureInfo resourceCulture;
        
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal CommonSqlStatements() {
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static System.Resources.ResourceManager ResourceManager {
            get {
                if (object.Equals(null, resourceMan)) {
                    System.Resources.ResourceManager temp = new System.Resources.ResourceManager("OpenMedStack.NEventStore.Persistence.Sql.SqlDialects.CommonSqlStatements", typeof(CommonSqlStatements).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        internal static string AppendSnapshotToCommit {
            get {
                return ResourceManager.GetString("AppendSnapshotToCommit", resourceCulture);
            }
        }
        
        internal static string DuplicateCommit {
            get {
                return ResourceManager.GetString("DuplicateCommit", resourceCulture);
            }
        }
        
        internal static string GetCommitsFromInstant {
            get {
                return ResourceManager.GetString("GetCommitsFromInstant", resourceCulture);
            }
        }
        
        internal static string GetCommitsFromToInstant {
            get {
                return ResourceManager.GetString("GetCommitsFromToInstant", resourceCulture);
            }
        }
        
        internal static string GetCommitsFromStartingRevision {
            get {
                return ResourceManager.GetString("GetCommitsFromStartingRevision", resourceCulture);
            }
        }
        
        internal static string GetSnapshot {
            get {
                return ResourceManager.GetString("GetSnapshot", resourceCulture);
            }
        }
        
        internal static string GetStreamsRequiringSnapshots {
            get {
                return ResourceManager.GetString("GetStreamsRequiringSnapshots", resourceCulture);
            }
        }
        
        internal static string PurgeStorage {
            get {
                return ResourceManager.GetString("PurgeStorage", resourceCulture);
            }
        }
        
        internal static string PurgeBucket {
            get {
                return ResourceManager.GetString("PurgeBucket", resourceCulture);
            }
        }
        
        internal static string DropTables {
            get {
                return ResourceManager.GetString("DropTables", resourceCulture);
            }
        }
        
        internal static string GetCommitsFromCheckpoint {
            get {
                return ResourceManager.GetString("GetCommitsFromCheckpoint", resourceCulture);
            }
        }
        
        internal static string GetCommitsFromBucketAndCheckpoint {
            get {
                return ResourceManager.GetString("GetCommitsFromBucketAndCheckpoint", resourceCulture);
            }
        }
        
        internal static string DeleteStream {
            get {
                return ResourceManager.GetString("DeleteStream", resourceCulture);
            }
        }
    }
}
