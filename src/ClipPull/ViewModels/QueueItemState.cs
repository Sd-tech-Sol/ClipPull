namespace ClipPull.ViewModels;

internal enum QueueItemState
{
    Waiting,
    Active,
    Completed,
    AlreadyDownloaded,
    Failed,
    Cancelled
}
