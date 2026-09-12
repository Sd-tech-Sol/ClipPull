using ClipPull.Services;
using ClipPull.ViewModels;

namespace ClipPull.Tests;

public sealed class QueueTests
{
    [Fact]
    public void WaitingAndFinishedItemsCanBeRemovedButActiveItemCannot()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ClipPull-queue-tests-{Guid.NewGuid():N}");
        try
        {
            using var viewModel = new MainViewModel(new SettingsService(directory), new UpdateService());
            var waiting = new QueueItemViewModel("https://example.com/waiting", "Web", "waiting", 0, 0);
            var completed = new QueueItemViewModel("https://example.com/completed", "Web", "completed", 0, 0)
            {
                State = QueueItemState.Completed
            };
            var active = new QueueItemViewModel("https://example.com/active", "Web", "active", 0, 0)
            {
                State = QueueItemState.Active
            };
            viewModel.Queue.Add(waiting);
            viewModel.Queue.Add(completed);
            viewModel.Queue.Add(active);

            viewModel.RemoveQueueItemCommand.Execute(waiting);
            viewModel.RemoveQueueItemCommand.Execute(completed);
            viewModel.RemoveQueueItemCommand.Execute(active);

            Assert.Single(viewModel.Queue);
            Assert.Same(active, viewModel.Queue[0]);
            Assert.False(active.CanRemove);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
