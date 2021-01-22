﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.UI.Windows.Components {
    
    using Utilities;
    
    public class ListComponent : ListBaseComponent, ISearchComponentByTypeEditor, ISearchComponentByTypeSingleEditor {

        System.Type ISearchComponentByTypeEditor.GetSearchType() {

            return typeof(ListComponentModule);

        }

        IList ISearchComponentByTypeSingleEditor.GetSearchTypeArray() {

            return this.componentModules.modules;

        }

        private bool HasCustomAddModule() {

            return false;

        }
        
        public override void AddItem<T, TClosure>(Resource source, TClosure closure, System.Action<T, TClosure> onComplete) {

            if (this.HasCustomAddModule() == true) {

                foreach (var module in this.componentModules.modules) {

                    if (module is ListComponentModule listModule && listModule.HasCustomAdd() == true) {

                        listModule.AddItem(source, closure, onComplete);
                        break;

                    }

                }

                this.OnElementsChanged();
                return;

            }
            
            base.AddItem(source, closure, onComplete);
            
        }

        public override void SetItems<T, TClosure>(int count, Resource source, System.Action<T, TClosure> onItem, TClosure closure, System.Action onComplete) {

            if (this.HasCustomAddModule() == true) {

                foreach (var module in this.componentModules.modules) {

                    if (module is ListComponentModule listModule && listModule.HasCustomAdd() == true) {

                        listModule.SetItems(count, source, onItem, closure, onComplete);
                        break;

                    }
                    
                }
                
                this.OnElementsChanged();
                return;

            }

            base.SetItems(count, source, onItem, closure, onComplete);
            
        }

    }

}