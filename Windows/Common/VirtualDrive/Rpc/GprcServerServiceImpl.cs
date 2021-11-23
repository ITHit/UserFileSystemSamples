using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;
using Grpc.Core;

namespace ITHit.FileSystem.Samples.Common.Windows.Rpc
{
    /// <summary>
    /// RPC methods handler.
    /// </summary>
    public class GprcServerServiceImpl : ShellExtensionRpc.ShellExtensionRpcBase
    {
        private static EmptyMessage EmptyMessage = new EmptyMessage();

        private VirtualEngineBase engine;
        private ILogger logger;

        public GprcServerServiceImpl(VirtualEngineBase engine, ILogger logger)
        {
            this.engine = engine;
            this.logger = logger;
        }

        /// <summary>
        /// Set lock/unlock status of files.
        /// </summary>
        public override async Task<EmptyMessage> SetLockStatus(ItemsStatusList request, ServerCallContext context)
        {
            bool failed = false;
            List<string> errorMessages = new List<string>();

            foreach (KeyValuePair<string, bool> pair in request.FilesStatus)
            {
                try
                {
                    string filePath = pair.Key;
                    bool fileStatus = pair.Value;

                    IClientNotificationsWindows clientNotifications = engine.ClientNotifications(filePath);
                    if (fileStatus)
                        await clientNotifications.LockAsync();
                    else
                        await clientNotifications.UnlockAsync();
                }
                catch (Exception ex)
                {
                    failed = true;
                    errorMessages.Add(ex.Message);
                    logger.LogError(ex.Message);
                }
            }

            if (failed)
                throw new RpcException(new Status(StatusCode.Internal, string.Join(". ", errorMessages)));

            return EmptyMessage;
        }

        /// <summary>
        /// Returns files lock status.
        /// </summary>
        public override async Task<ItemsStatusList> GetLockStatus(ItemsPathList request, ServerCallContext context)
        {
            try
            {
                ItemsStatusList itemsStatusList = new ItemsStatusList();

                foreach (string filePath in request.Files)
                {
                    IClientNotificationsWindows clientNotifications = engine.ClientNotifications(filePath);
                    LockMode lockMode = await clientNotifications.GetLockModeAsync();
                    bool lockStatus = lockMode != LockMode.None;

                    itemsStatusList.FilesStatus.Add(filePath, lockStatus);
                }

                return itemsStatusList;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        /// <summary>
        /// Returns Thumbnail.
        /// </summary>
        public override async Task<Thumbnail> GetThumbnail(ThumbnailRequest thumbnailRequest, ServerCallContext context)
        {
            if (thumbnailRequest == null)
            {
                throw new RpcException(new Status(StatusCode.Internal, "thumbnailRequest is null"));
            }

            string path = thumbnailRequest.Path;
            uint size = thumbnailRequest.Size;

            try
            {
                byte[] bitmap = await engine.GetThumbnailAsync(path, size);
                if ((bitmap == null) || (bitmap.Length == 0))
                {
                    throw new NotImplementedException();
                }

                Thumbnail thumbnail = new Thumbnail();
                thumbnail.Image = Google.Protobuf.ByteString.CopyFrom(bitmap);

                return thumbnail;
            }
            catch (NotImplementedException)
            {
                string msg = $"Thumbnail for {path} is not implemented";
                logger.LogMessage(msg);
                throw new RpcException(new Status(StatusCode.Internal, msg));
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }
    }
}
