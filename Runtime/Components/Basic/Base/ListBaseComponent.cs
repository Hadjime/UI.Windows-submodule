﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.UI.Windows.Components {

    using Utilities;
    
    public interface IListClosureParameters {

        int index { get; set; }

    }

    public abstract class ListBaseComponent : WindowComponent, ILayoutSelfController, UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IEndDragHandler {

        [UnityEngine.UI.Windows.Modules.ResourceTypeAttribute(typeof(WindowComponent), RequiredType.Warning)]
        public Resource source;
        public Transform customRoot;

        public List<WindowComponent> items = new List<WindowComponent>();
        private HashSet<Object> loadedAssets = new HashSet<Object>();
        private System.Action onElementsChangedCallback;
        private System.Action onLayoutChangedCallback;

        [SerializeField]
        internal ListRectTransformChangedInternal listRectTransformChangedInternal;

        private void ValidateEditorRectTransformInternal() {

            if (this.listRectTransformChangedInternal != null && this.listRectTransformChangedInternal.listBaseComponent == null) {

                this.listRectTransformChangedInternal.listBaseComponent = this;
                this.listRectTransformChangedInternal.hideFlags = HideFlags.HideInInspector;

            }
            
            if (this.listRectTransformChangedInternal == null) {

                var tr = this.customRoot;
                if (tr == null) tr = this.transform;
                this.listRectTransformChangedInternal = tr.gameObject.AddComponent<ListRectTransformChangedInternal>();
                this.listRectTransformChangedInternal.listBaseComponent = this;
                this.listRectTransformChangedInternal.hideFlags = HideFlags.HideInInspector;

            } else {

                var tr = this.customRoot;
                if (tr == null) tr = this.transform;
                if (this.listRectTransformChangedInternal.transform != tr) {
                    
                    Object.DestroyImmediate(this.listRectTransformChangedInternal);
                    this.listRectTransformChangedInternal = null;
                    this.ValidateEditorRectTransformInternal();

                }
                
            }

        }
        
        #if UNITY_EDITOR
        public override void ValidateEditor() {
            
            base.ValidateEditor();

            this.ValidateEditorRectTransformInternal();
            
            var editorObj = this.source.GetEditorRef<WindowComponent>();
            if (editorObj != null) {
            
                if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(editorObj) == false) {

                    editorObj.allowRegisterInRoot = false;
                    editorObj.AddEditorParametersRegistry(new EditorParametersRegistry() {
                        holder = this,
                        allowRegisterInRoot = true,
                        allowRegisterInRootDescription = "Hold by ListComponent"
                    });

                    editorObj.gameObject.SetActive(false);

                }

            }
            
        }
        #endif

        void ILayoutController.SetLayoutHorizontal() {

            this.OnLayoutChanged();

        }

        void ILayoutController.SetLayoutVertical() {
            
            this.OnLayoutChanged();
            
        }

        internal void ForceLayoutChange() {
            
            this.OnLayoutChanged();
            
        }
        
        public int Count {
            get {
                return this.items.Count;
            }
        }

        protected virtual void OnLayoutChanged() {

            this.componentModules.OnLayoutChanged();
            if (this.onLayoutChangedCallback != null) this.onLayoutChangedCallback.Invoke();

        }

        public void SetOnLayoutChangedCallback(System.Action callback) {

            this.onLayoutChangedCallback = callback;

        }
        
        public void SetOnElementsCallback(System.Action callback) {

            this.onElementsChangedCallback = callback;

        }
        
        public override void OnInit() {
            
            base.OnInit();

            WindowSystem.onPointerUp += this.OnPointerUp;

        }

        public override void OnDeInit() {
            
            base.OnDeInit();

            this.onElementsChangedCallback = null;
            this.onLayoutChangedCallback = null;
            
            WindowSystem.onPointerUp -= this.OnPointerUp;
            
            var resources = WindowSystem.GetResources();
            foreach (var asset in this.loadedAssets) {
            
                resources.Delete(this, asset);

            }
            this.loadedAssets.Clear();
            
        }
        
        private void OnPointerUp() {
            
            var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            for (int i = 0; i < this.componentModules.modules.Length; ++i) {

                var module = this.componentModules.modules[i] as ListComponentModule;
                if (module == null) continue;
                
                module.OnDragEnd(eventData);

            }

        }
        
        void UnityEngine.EventSystems.IBeginDragHandler.OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData) {
            
            for (int i = 0; i < this.componentModules.modules.Length; ++i) {

                var module = this.componentModules.modules[i] as ListComponentModule;
                if (module == null) continue;
                
                module.OnDragBegin(eventData);

            }
            
        }

        void UnityEngine.EventSystems.IDragHandler.OnDrag(UnityEngine.EventSystems.PointerEventData eventData) {
            
            for (int i = 0; i < this.componentModules.modules.Length; ++i) {

                var module = this.componentModules.modules[i] as ListComponentModule;
                if (module == null) continue;
                
                module.OnDragMove(eventData);

            }

        }

        void UnityEngine.EventSystems.IEndDragHandler.OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData) {
            
            for (int i = 0; i < this.componentModules.modules.Length; ++i) {

                var module = this.componentModules.modules[i] as ListComponentModule;
                if (module == null) continue;
                
                module.OnDragEnd(eventData);

            }

        }

        public Transform GetRoot() {

            if (this.customRoot != null) return this.customRoot;
            
            return this.transform;

        }

        public virtual void Clear() {

            this.RemoveRange(0, this.items.Count);
            
        }
        
        public void RemoveRange(int from, int to) {
            
            var pools = WindowSystem.GetPools();
            for (int i = to - 1; i >= from; --i) {

                this.UnRegisterSubObject(this.items[i]);
                pools.Despawn(this.items[i]);
                this.NotifyModulesComponentRemoved(this.items[i]);
                
            }
            this.items.RemoveRange(from, to - from);
            this.OnElementsChanged();

        }
        
        public virtual void OnElementsChanged() {

            for (int i = 0; i < this.componentModules.modules.Length; ++i) {

                var module = this.componentModules.modules[i] as ListComponentModule;
                if (module == null) continue;
                
                module.OnComponentsChanged();

            }
            
            if (this.onElementsChangedCallback != null) this.onElementsChangedCallback.Invoke();
            
        }
        
        public virtual T GetItem<T>(int index) where T : WindowComponent {

            return this.items[index] as T;

        }
        
        public virtual void AddItem(System.Action<WindowComponent, DefaultParameters> onComplete = null) {
            
            this.AddItem(this.source, new DefaultParameters(), onComplete);
            
        }

        public virtual void AddItem<T>(Resource source, System.Action<T, DefaultParameters> onComplete = null) where T : WindowComponent {
            
            this.AddItem(source, new DefaultParameters(), onComplete);
            
        }

        public virtual void AddItem<T>(System.Action<T, DefaultParameters> onComplete = null) where T : WindowComponent {
            
            this.AddItem(this.source, new DefaultParameters(), onComplete);
            
        }

        public virtual void AddItem<T, TClosure>(Resource source, TClosure closure, System.Action<T, TClosure> onComplete) where T : WindowComponent {

            this.AddItemInternal(source, closure, onComplete);

        }

        internal void AddItemInternal<T, TClosure>(Resource source, TClosure closure, System.Action<T, TClosure> onComplete) where T : WindowComponent {
            
            var resources = WindowSystem.GetResources();
            var pools = WindowSystem.GetPools();
            Coroutines.Run(resources.LoadAsync<T>(this, source, (asset) => {

                if (this.loadedAssets.Contains(asset) == false) this.loadedAssets.Add(asset);
                
                var instance = pools.Spawn(asset, this.GetRoot());
                this.RegisterSubObject(instance);
                this.items.Add(instance);
                this.NotifyModulesComponentAdded(instance);
                this.OnElementsChanged();
                if (onComplete != null) onComplete.Invoke(instance, closure);

            }));

        }

        public virtual void RemoveAt(int index) {

            if (index < this.items.Count) {

                var pools = WindowSystem.GetPools();
                this.UnRegisterSubObject(this.items[index]);
                pools.Despawn(this.items[index]);
                this.NotifyModulesComponentRemoved(this.items[index]);
                this.items.RemoveAt(index);
                this.OnElementsChanged();

            }

        }

        public struct DefaultParameters : IListClosureParameters {

            public int index { get; set; }

        }
        public virtual void SetItems<T>(int count, System.Action<T, DefaultParameters> onItem, System.Action onComplete = null) where T : WindowComponent {
            
            this.SetItems(count, this.source, onItem, new DefaultParameters(), onComplete);
            
        }

        public virtual void SetItems<T, TClosure>(int count, System.Action<T, TClosure> onItem, TClosure closure, System.Action onComplete = null) where T : WindowComponent where TClosure : IListClosureParameters {
            
            this.SetItems(count, this.source, onItem, closure, onComplete);
            
        }

        private bool isLoadingRequest = false;
        public virtual void SetItems<T, TClosure>(int count, Resource source, System.Action<T, TClosure> onItem, TClosure closure, System.Action onComplete) where T : WindowComponent where TClosure : IListClosureParameters {

            if (this.isLoadingRequest == true) {

                return;

            }
            
            if (count == this.Count) {

                for (int i = 0; i < this.Count; ++i) {

                    closure.index = i;
                    onItem.Invoke((T)this.items[i], closure);
                    
                }

                if (onComplete != null) onComplete.Invoke();

            } else {

                var delta = count - this.Count;
                if (delta > 0) {

                    this.Emit(delta, source, onItem, closure, onComplete);

                } else {
                    
                    this.RemoveRange(this.Count + delta, this.Count);
                    for (int i = 0; i < this.Count; ++i) {
                    
                        closure.index = i;
                        onItem.Invoke((T)this.items[i], closure);
                    
                    }
                    if (onComplete != null) onComplete.Invoke();
                    
                }

            }

        }

        private struct EmitClosure<T, TClosure> {

            public int index;
            public ListBaseComponent list;
            public int requiredCount;
            public System.Action<T, TClosure> onItem;
            public System.Action onComplete;
            public TClosure data;

        }
        private void Emit<T, TClosure>(int count, Resource source, System.Action<T, TClosure> onItem, TClosure closure, System.Action onComplete = null) where T : WindowComponent where TClosure : IListClosureParameters {

            if (count == 0) {
                
                if (onComplete != null) onComplete.Invoke();
                this.isLoadingRequest = false;
                return;

            }
            
            var closureInner = new EmitClosure<T, TClosure>();
            closureInner.data = closure;
            closureInner.onItem = onItem;
            closureInner.onComplete = onComplete;
            closureInner.list = this;
            closureInner.requiredCount = count;
            
            this.isLoadingRequest = true;
            var offset = this.Count;
            var loaded = 0;
            for (int i = 0; i < count; ++i) {

                var index = i + offset;
                closureInner.index = index;
                
                this.AddItemInternal<T, EmitClosure<T, TClosure>>(source, closureInner, (item, c) => {

                    c.data.index = c.index;
                    c.onItem.Invoke(item, c.data);
                    
                    ++loaded;
                    if (loaded == c.requiredCount) {
                        
                        if (c.onComplete != null) c.onComplete.Invoke();
                        c.list.isLoadingRequest = false;
                        
                    }
                    
                });
                
            }
            
        }
        
        private void NotifyModulesComponentAdded(WindowComponent component) {
            
            foreach (var module in this.componentModules.modules) {
            
                (module as ListComponentModule)?.OnComponentAdded(component);
                
            }
            
        }
        
        private void NotifyModulesComponentRemoved(WindowComponent component) {
            
            foreach (var module in this.componentModules.modules) {
                
                (module as ListComponentModule)?.OnComponentRemoved(component);
                
            }
            
        }

    }

}