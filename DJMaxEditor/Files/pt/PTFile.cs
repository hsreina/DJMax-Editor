using System;
using System.IO;
using System.Linq;
using UnpackMe.SDK.Core;
using UnpackMe.SDK.Core.Models;

namespace DJMaxEditor.Files.pt
{
    internal abstract class PTFile
    {
        protected const uint EZTR = 0x52545A45;

        protected bool TryDoStuffDataOnline(byte[] data, string mode, out byte[] result)
        {
            result = null;
            try
            {
                result = DoStuffDataOnline(data, mode);
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        protected byte[] DoStuffDataOnline(byte[] data, string mode)
        {
            using (UnpackMeClient unpackMeClient = new UnpackMeClient(m_unpackMeUrl))
            {
                unpackMeClient.Authenticate(m_unpackMeClientLogin, m_unpackMeClientPassword);
                var commands = unpackMeClient.GetAvailableCommands();

                var commandName = mode == "decrypt" ? "DJMax *.pt decrypt" : "DJMax *.pt encrypt";

                var decryptCommand = commands.SingleOrDefault(x => x.CommandTitle == commandName);

                using (var stream = new MemoryStream(data))
                {
                    var taskId = unpackMeClient.CreateTaskFromCommandId(decryptCommand.CommandId, stream);

                    TaskModel task;
                    string taskStatus;
                    do
                    {
                        task = unpackMeClient.GetTaskById(taskId);
                        taskStatus = task.TaskStatus;

                        System.Threading.Thread.Sleep(500);

                    } while (taskStatus != "completed");

                    return unpackMeClient.DownloadToByteArray(taskId);
                }
            }
        }

        private const string m_unpackMeUrl = "http://api.unpackme.shadosoft-tm.com/";

        private const string m_unpackMeClientLogin = "djmaxeditor";

        private const string m_unpackMeClientPassword = "djmaxeditor";
    }
}
