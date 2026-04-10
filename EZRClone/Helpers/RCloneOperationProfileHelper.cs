using HotCoreUtility.RClone;
using EZRClone.Models;

namespace EZRClone.Helpers;

public static class RCloneOperationProfileHelper
{
    public static RCloneOperationProfile GetJobProfile(RCloneOperation operation)
    {
        return operation switch
        {
            RCloneOperation.Copy => RCloneOperationProfile.JobCopy,
            RCloneOperation.Sync => RCloneOperationProfile.JobSync,
            RCloneOperation.Move => RCloneOperationProfile.JobMove,
            RCloneOperation.Delete => RCloneOperationProfile.JobDelete,
            _ => RCloneOperationProfile.JobCopy
        };
    }
}
