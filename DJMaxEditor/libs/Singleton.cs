using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DJMaxEditor.libs {

    public class Singleton<T> where T : class, new() {

        public static T GetInstance() {

            if (null == _instance) {
                Type type = typeof(T);
                _instance = (T)Activator.CreateInstance(type, true);
            }

            return _instance;
        }

        private static T _instance;

    }
}
