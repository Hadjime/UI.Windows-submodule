﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.UI.Windows {

    public enum UnloadResourceEventType {

        Manually,
        OnHideBegin,
        OnHideEnd,
        OnDeInit,

    }

    public interface IResourceProvider {

        Resource GetResource();

    }

    [System.Serializable]
    public class Resource<T> : IResourceProvider where T : UnityEngine.Object {

        [SerializeField]
        private Resource data;
        private T loaded;
        
        internal Resource() { }
        
        Resource IResourceProvider.GetResource() => this.data;

        #if UNITY_EDITOR
        public void ValidateSource(T resource) {
            
            var res = Resource<T>.Validate(resource);
            this.data = res.data;
            this.loaded = res.loaded;

        }
        
        public static Resource<T> Validate(T resource) {
            
            var res = new Resource<T>();
            res.data.directRef = resource;
            res.data.objectType = Resource.ObjectType.Unknown;
            res.data.guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(resource));
            res.data.type = Resource.Type.Direct;
            res.data.validationRequired = true;
            return res;

        }
        #endif

        public T Load(object handler) {

            if (this.loaded != null) return this.loaded;
            this.loaded = WindowSystem.GetResources().Load<T>(handler, this.data);
            return this.loaded;

        }

        public T Get() {

            return this.loaded;

        }

        public void Unload(object handler) {

            if (this.loaded == null) return;
            WindowSystem.GetResources().Delete(handler, ref this.loaded);

        }
        
        public static implicit operator T(Resource<T> item) {

            return item.loaded;

        }

    }
    
    [System.Serializable]
    public struct Resource : System.IEquatable<Resource> {

        public enum ObjectType {

            Unknown = 0,
            GameObject,
            Component,
            ScriptableObject,
            Sprite,
            Texture,

        }
        
        public enum Type {

            Manual = 0,
            Direct,
            Addressables,

        }

        public Type type;
        public ObjectType objectType;
        public string address;
        public string guid;
        public string subObjectName;
        public Object directRef;
        public bool validationRequired;

        public bool Equals(Resource other) {

            return this.type == other.type &&
                this.objectType == other.objectType &&
                this.address == other.address &&
                this.guid == other.guid &&
                this.subObjectName == other.subObjectName &&
                this.directRef == other.directRef;

        }

        public string GetAddress() {

            if (string.IsNullOrEmpty(this.address) == false && string.IsNullOrEmpty(this.address.Trim()) == false) return this.address;
            return this.guid;

        }

        public override string ToString() {
            
            return $"[Resource] Type: {this.type}, Object Type: {this.objectType}, GUID: {this.guid} ({this.subObjectName}), Direct Reference: {this.directRef}";
            
        }

        public override int GetHashCode() {
            
            return (int)this.type ^ (int)this.objectType ^ (this.guid != null ? this.guid.GetHashCode() : 0) ^ (this.directRef != null ? this.directRef.GetHashCode() : 0);
            
        }

        public bool IsEquals(in Resource other) {

            return this.type == other.type &&
                   this.objectType == other.objectType &&
                   this.address == other.address &&
                   this.guid == other.guid &&
                   this.subObjectName == other.subObjectName &&
                   this.directRef == other.directRef;

        }
        
        public bool IsEmpty() {

            return this.directRef == null && string.IsNullOrEmpty(this.guid) == true;

        }

        public T GetEditorRef<T>() where T : Object {
            
            return Resource.GetEditorRef<T>(this);

        }

        public static T GetEditorRef<T>(Resource resource) where T : Object {

            return Resource.GetEditorRef<T>(resource.guid, resource.subObjectName, resource.objectType, resource.directRef);

        }

        public static T GetEditorRef<T>(string guid, string subObjectName, ObjectType objectType, Object directRef) where T : Object {

            return Resource.GetEditorRef(guid, subObjectName, typeof(T), objectType, directRef) as T;

        }

        public static Object GetEditorRef(string guid, string subObjectName, System.Type type, ObjectType objectType, Object directRef) {

            #if UNITY_EDITOR
            if (directRef != null) return directRef;
            
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (objectType == ObjectType.Component) {
                
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) return default;
                
                if (type == typeof(Object)) type = typeof(Component);
                return go.GetComponent(type);

            } else if (objectType == ObjectType.Sprite) {
                
                var objs = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var obj in objs) {

                    if (obj.name == subObjectName && obj.GetType().IsSubclassOf(type) == true) {

                        return obj;

                    }
                    
                }
                
            }
            return UnityEditor.AssetDatabase.LoadAssetAtPath(path, type);
            #else
            return default;
            #endif

        }

    }

}

namespace UnityEngine.UI.Windows.Modules {

    using Utilities;
    
    public interface IResourceConstructor<T> where T : class {

        T Construct();
        void Deconstruct(ref T obj);

    }

    public struct DefaultConstructor<T> : IResourceConstructor<T> where T : class, new() {

        public T Construct() {

            return new T();

        }

        public void Deconstruct(ref T obj) {

            obj = default;

        }

    }

    public class ResourceTypeAttribute : PropertyAttribute {

        public System.Type type;
        public RequiredType required;

        public ResourceTypeAttribute(System.Type type, RequiredType required = RequiredType.None) {

            this.type = type;
            this.required = required;

        }

    }

    public class WindowSystemResources : MonoBehaviour {

        public readonly struct InternalTask : System.IEquatable<InternalTask> {

            public readonly int resourceId;
            public readonly Resource resourceSource;
            
            public InternalTask(Resource resourceSource) {

                this.resourceId = resourceSource.GetHashCode();
                this.resourceSource = resourceSource;

            }

            public bool Equals(InternalTask other) {

                return other.resourceId == this.resourceId;

            }

            public override int GetHashCode() {

                return this.resourceId;

            }

        }

        public struct DefaultClosureData { }

        public class ClosureResult<T> {

            public T result;
            
        }

        public struct LoadParameters {

            public bool async;

        }

        public class IntResource {

            public object loaded;
            public Resource resource;
            public List<object> references;
            public HashSet<object> handlers;
            public System.Action deconstruct;

            public int referencesCount => this.references.Count;

            public void Reset() {
                
                this.loaded = null;
                this.resource = default;
                this.references = null;
                this.handlers = null;
                this.deconstruct = null;
                
            }

        }

        private readonly Dictionary<InternalTask, System.Action<object>> tasks = new Dictionary<InternalTask, System.Action<object>>();
        private readonly Dictionary<int, HashSet<System.Action>> handlerToTasks = new Dictionary<int, HashSet<System.Action>>();
        private readonly Dictionary<Resource, IntResource> loaded = new Dictionary<Resource, IntResource>();
        private readonly Dictionary<object, IntResource> loadedObjCache = new Dictionary<object, IntResource>();
        private readonly List<object> internalDeleteAllCache = new List<object>();

        public Dictionary<Resource, IntResource> GetAllObjects() {

            return this.loaded;

        }

        public int GetAllocatedCount() {

            return this.loaded.Count;

        }

        public Dictionary<InternalTask, System.Action<object>> GetTasks() {

            return this.tasks;

        }
        
        private bool RequestLoad<T, TClosure>(object handler, TClosure closure, Resource resource, System.Action<T, TClosure> onComplete) where T : class {
            
            var item = new InternalTask(resource);
            if (this.tasks.TryGetValue(item, out var onCompleteActions) == true) {

                onCompleteActions += (obj) => onComplete.Invoke((T)obj, closure);
                this.tasks[item] = onCompleteActions;
                return true;

            } else {
                
                onCompleteActions += (obj) => onComplete.Invoke((T)obj, closure);
                this.tasks.Add(item, onCompleteActions);
                
            }

            return false;

        }

        private void CompleteTask(object handler, Resource resource, object result) {

            var item = new InternalTask(resource);
            if (this.tasks.TryGetValue(item, out var onCompleteActions) == true) {

                try {
                    onCompleteActions.Invoke(result);
                } catch (System.Exception ex) {
                    Debug.LogException(ex);
                }

                this.tasks.Remove(item);

            }

        }

        public IEnumerator LoadAsync<T, TClosure>(LoadParameters loadParameters, object handler, TClosure closure, Resource resource, System.Action<T, TClosure> onComplete) where T : class {
            
            yield return this.Load_INTERNAL(loadParameters, handler, closure, resource, onComplete);
            
        }

        public IEnumerator LoadAsync<T>(object handler, Resource resource, System.Action<T, DefaultClosureData> onComplete) where T : class {

            yield return this.LoadAsync<T, DefaultClosureData>(handler, new DefaultClosureData(), resource, onComplete);

        }

        public IEnumerator LoadAsync<T, TClosure>(object handler, TClosure closure, Resource resource, System.Action<T, TClosure> onComplete) where T : class {
            
            yield return this.Load_INTERNAL(new LoadParameters() { async = true }, handler, closure, resource, onComplete);
            
        }

        public T Load<T>(object handler, Resource resource) where T : class {
            
            var closure = PoolClass<ClosureResult<T>>.Spawn();
            var op = this.Load_INTERNAL<T, ClosureResult<T>>(new LoadParameters() { async = false }, handler, closure, resource, (asset, c) => {

                c.result = asset;

            });
            while (op.MoveNext() == true) { }

            var result = closure.result;
            closure.result = null;
            PoolClass<ClosureResult<T>>.Recycle(ref closure);
            return result;

        }

        private static void ReleaseAddressableAsset<T>(T obj) {
            
            UnityEngine.AddressableAssets.Addressables.Release(obj);
            
        }

        private bool IsLoaded(object handler, Resource resource) {

            if (this.loaded.TryGetValue(resource, out var internalResource) == true) {

                this.AddObject(handler, internalResource.loaded, resource, internalResource.deconstruct);
                this.CompleteTask(handler, resource, internalResource.loaded);
                return true;
                
            }

            return false;

        }

        private IEnumerator Load_INTERNAL<T, TClosure>(LoadParameters loadParameters, object handler, TClosure closure, Resource resource, System.Action<T, TClosure> onComplete) where T : class {

            if (typeof(Component).IsAssignableFrom(typeof(T)) == true) {
                        
                resource.objectType = Resource.ObjectType.Component;
                        
            }

            if (this.RequestLoad(handler, closure, resource, onComplete) == true) {
                
                // Waiting for loading then break
                var item = new InternalTask(resource);
                while (this.tasks.ContainsKey(item) == true) yield return null;
                yield break;
                
            }

            if (this.IsLoaded(handler, resource) == true) {
                
                yield break;
                
            }
            
            switch (resource.type) {
                
                case Resource.Type.Manual: {

                    this.CompleteTask(handler, resource, default);

                    break;

                }

                case Resource.Type.Direct: {

                    if (resource.directRef is GameObject go && typeof(T).IsAssignableFrom(typeof(Component))) {

                        var direct = go.GetComponent<T>();
                        this.AddObject(handler, direct, resource, null);

                        this.CompleteTask(handler, resource, direct);
                        yield break;

                    } else if (resource.directRef is T direct) {

                        this.AddObject(handler, direct, resource, null);

                        this.CompleteTask(handler, resource, direct);
                        yield break;

                    }

                    this.CompleteTask(handler, resource, default);

                    break;

                }

                case Resource.Type.Addressables: {

                    if (resource.objectType == Resource.ObjectType.Component) {

                        var op = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(resource.GetAddress());
                        System.Action cancellationTask = () => { if (op.IsValid() == true) UnityEngine.AddressableAssets.Addressables.Release(op); };
                        this.LoadBegin(handler, cancellationTask);
                        if (loadParameters.async == false) op.WaitForCompletion();
                        while (op.IsDone == false) yield return null;

                        if (op.IsValid() == false) {
                            
                            //this.CompleteTask(handler, resource, default);
                            
                        } else {

                            if (op.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) {

                                var asset = op.Result;
                                if (asset == null) {

                                    this.CompleteTask(handler, resource, default);

                                } else {

                                    var result = asset.GetComponent<T>();
                                    this.AddObject(handler, result, resource, () => WindowSystemResources.ReleaseAddressableAsset(asset));
                                    this.CompleteTask(handler, resource, result);

                                }

                            }

                        }

                        this.LoadEnd(handler, cancellationTask);

                    } else {

                        UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<T> op;
                        if (string.IsNullOrEmpty(resource.subObjectName) == false) {
                            
                            op = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>($"{resource.GetAddress()}[{resource.subObjectName}]");
                            
                        } else {
                            
                            op = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(resource.address ?? resource.guid);
                            
                        }
                        
                        System.Action cancellationTask = () => { if (op.IsValid() == true) UnityEngine.AddressableAssets.Addressables.Release(op); };
                        this.LoadBegin(handler, cancellationTask);
                        if (loadParameters.async == false) op.WaitForCompletion();
                        while (op.IsDone == false) yield return null;

                        if (op.IsValid() == false) {
                            
                            //this.CompleteTask(handler, resource, default);
                            
                        } else {
                            
                            if (op.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) {

                                var asset = op.Result;
                                if (asset == null) {

                                    this.CompleteTask(handler, resource, default);

                                } else {

                                    this.AddObject(handler, asset, resource, () => WindowSystemResources.ReleaseAddressableAsset(asset));
                                    this.CompleteTask(handler, resource, asset);

                                }

                            }

                        }
                        
                        this.LoadEnd(handler, cancellationTask);

                    }

                    break;

                }

            }

        }

        private void LoadBegin(object handler, System.Action cancellationTask) {

            var key = handler.GetHashCode();
            if (this.handlerToTasks.TryGetValue(key, out var list) == true) {
                
                list.Add(cancellationTask);
                
            } else {
                
                list = new HashSet<System.Action>();
                list.Add(cancellationTask);
                this.handlerToTasks.Add(key, list);
                
            }

        }

        private void LoadEnd(object handler, System.Action task) {

            this.handlerToTasks[handler.GetHashCode()].Remove(task);

        }

        public void StopLoadAll(object handler) {

            var key = handler.GetHashCode();
            if (this.handlerToTasks.TryGetValue(key, out var list) == true) {

                foreach (var item in list) {

                    item.Invoke();

                }
                
            }

        }

        public T New<T>() where T : class, new() {

            return this.New<T, DefaultConstructor<T>>(null, new DefaultConstructor<T>());

        }

        public T New<T, TConstruct>(TConstruct resourceConstructor) where T : class, new() where TConstruct : IResourceConstructor<T> {

            return this.New<T, TConstruct>(null, resourceConstructor);

        }

        public T New<T>(object handler) where T : class, new() {

            return this.New<T, DefaultConstructor<T>>(handler, new DefaultConstructor<T>());

        }

        public T New<T, TConstruct>(object handler, TConstruct resourceConstructor) where T : class where TConstruct : IResourceConstructor<T> {

            if (handler == null) handler = this;

            var obj = resourceConstructor.Construct();
            this.AddObject(handler, obj, new Resource() { type = Resource.Type.Manual }, () => resourceConstructor.Deconstruct(ref obj));
            return obj;

        }

        public void Delete<T>(T obj) where T : class {

            this.Delete(null, ref obj);

        }

        public void Delete<T>(ref T obj) where T : class {

            this.Delete(null, ref obj);

        }

        public void Delete<T>(object handler, T obj) where T : class {

            this.Delete(handler, ref obj);

        }

        public void Delete<T>(object handler, ref T obj) where T : class {

            if (obj == null) return;
            if (handler == null) handler = this;

            //Debug.Log("Delete obj: " + handler + " :: " + obj);
            this.RemoveObject(handler, obj);
            obj = null;

        }

        public void DeleteAll(object handler) {

            if (handler == null) handler = this;

            this.internalDeleteAllCache.Clear();
            foreach (var kv in this.loaded) {

                if (kv.Value.handlers.Contains(handler) == true) {

                    this.internalDeleteAllCache.Add(kv.Value.loaded);

                }
                
            }

            foreach (var obj in this.internalDeleteAllCache) {

                this.RemoveObject(handler, obj);

            }
            this.internalDeleteAllCache.Clear();
            
        }

        private bool RemoveObject<T>(object handler, T obj) where T : class {

            if (this.loadedObjCache.TryGetValue(obj, out var intResource) == true) {

                if (intResource.handlers.Contains(handler) == true) {

                    var hidx = 0;
                    var count = 0;
                    for (int i = 0; i < intResource.references.Count; ++i) {

                        if (intResource.references[i] == handler) {
                            
                            hidx = i;
                            ++count;

                        }
                        
                    }
                    intResource.references.RemoveAt(hidx);
                    if (count == 1) intResource.handlers.Remove(handler);
                    
                    if (intResource.referencesCount == 0) {

                        intResource.handlers.Remove(handler);
                        intResource.deconstruct?.Invoke();
                        this.loaded.Remove(intResource.resource);
                        this.loadedObjCache.Remove(obj);
                        PoolHashSet<object>.Recycle(ref intResource.handlers);
                        PoolList<object>.Recycle(ref intResource.references);
                        intResource.Reset();
                        PoolClass<IntResource>.Recycle(intResource);
                        return true;
                        
                    }

                }
                
            }

            return false;

        }

        private void AddObject(object handler, object obj, Resource resource, System.Action deconstruct) {

            if (this.loaded.TryGetValue(resource, out var intResource) == false) {

                intResource = PoolClass<IntResource>.Spawn();
                intResource.handlers = PoolHashSet<object>.Spawn();
                intResource.references = PoolList<object>.Spawn();
                intResource.loaded = obj;
                intResource.resource = resource;
                intResource.deconstruct = deconstruct;
                this.loaded.Add(resource, intResource);
                this.loadedObjCache.Add(obj, intResource);

            }
            
            intResource.handlers.Add(handler);
            intResource.references.Add(handler);

        }

    }

}