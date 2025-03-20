using CommunityToolkit.Mvvm.ComponentModel;

using ITHit.FileSystem;


namespace WebDAVDrive.ViewModels
{
    /// <summary>
    /// View model representing history item of event (to show on error details window).
    /// </summary>
    public class HistoryItemViewModel : ObservableObject
    {
        private OperationStatus operationStatus;

        /// <summary>
        /// Operation status (e.g. Failed or Conflict)
        /// </summary>
        public OperationStatus OperationStatus
        {
            get { return operationStatus; }
            set
            {
                SetProperty(ref operationStatus, value);
            }
        }
       

        private string? errorMessage;

        /// <summary>
        /// Is filled for operations with error. Contains error message. 
        /// For successed events it is null.
        /// </summary>
        public string? ErrorMessage
        {
            get { return errorMessage; }
            set
            {
                SetProperty(ref errorMessage, value);                
            }
        }

        private string? exceptionStackTrace;

        /// <summary>
        /// In case this item is related to exception - contains its stack trace. Otherwise null.
        /// </summary>
        public string? ExceptionStackTrace {
            get { return exceptionStackTrace; }
            set
            {
                SetProperty(ref exceptionStackTrace, value);
            }
        }
    }
}
