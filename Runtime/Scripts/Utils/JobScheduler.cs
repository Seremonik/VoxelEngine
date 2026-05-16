using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Jobs;

namespace VoxelEngine
{
    public class JobHandleNotifier
    {
        public JobHandle JobHandle;
        public readonly TaskCompletionSource<bool> TaskCompletionSource;
        public readonly Action CompleteAction;

        public JobHandleNotifier(JobHandle jobHandle, TaskCompletionSource<bool> taskCompletionSource)
        {
            JobHandle = jobHandle;
            TaskCompletionSource = taskCompletionSource;
        }
        public JobHandleNotifier(JobHandle jobHandle, Action completeAction)
        {
            JobHandle = jobHandle;
            CompleteAction = completeAction;
        }
    }
    
    /// <summary>
    /// Helper class that Schedules the jobs and notifies when they finish
    /// </summary>
    public class JobScheduler
    {
        private readonly List<JobHandleNotifier> scheduledJobs = new ();

        public void LateUpdate()
        {
            for (int i = scheduledJobs.Count-1; i >= 0; i--)
            {
                if (!scheduledJobs[i].JobHandle.IsCompleted)
                    continue;
                
                scheduledJobs[i].JobHandle.Complete();
                scheduledJobs[i].TaskCompletionSource?.TrySetResult(true);
                scheduledJobs[i].CompleteAction?.Invoke();
                scheduledJobs.RemoveAt(i);
            }
        }

        public void ScheduleJob(JobHandle jobHandle, TaskCompletionSource<bool> tcs)
        {
            scheduledJobs.Add(new JobHandleNotifier(jobHandle, tcs));
        }
        
        public void ScheduleJob(JobHandle jobHandle, Action jobFinished)
        {
            scheduledJobs.Add(new JobHandleNotifier(jobHandle, jobFinished));
        }
    }
}