using System;
using System.Collections.Generic;
using System.Linq;

namespace DJMaxEditor.Files
{
    internal abstract class FilesHandler<Type> where Type : IFile
    {
        protected IList<Type> _handlers;

        public FilesHandler()
        {
            _handlers = new List<Type>();
        }

        public void Register(Type handler)
        {
            _handlers.Add(handler);
        }

        public string GetFilter()
        {
            return String.Join("|", _handlers.Select(x => $"{x.GetDescription()}|*.{x.GetExtension()}"));
        }

        public string GetDefaultExtension()
        {
            var firstEntry = _handlers.FirstOrDefault();
            if (firstEntry == null)
            {
                return String.Empty;
            }
            return $"*.{firstEntry.GetExtension()}";
        }

        public Type GetHandlerForExtension(string extension)
        {
            return _handlers.FirstOrDefault(x => $".{x.GetExtension()}" == extension);
        }

        public Type GetHandlerForFilterIndex(int filterIndex) 
        {
            int index = filterIndex - 1;
            if ((index < 0) || (index > _handlers.Count - 1)) 
            {
                return default;
            }
            // Todo: fix this for the case of multiple extension for the same Handler
            return _handlers[filterIndex - 1];
        }
    }
}
