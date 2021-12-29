namespace OpenMedStack.NEventStore
{
    public static class PipelineHookExtensions
    {
        /// <summary>
        ///     Invoked when all buckets have been purged.
        /// </summary>
        /// <param name="pipelineHook">The pipeline hook.</param>
        public static void OnPurge(this IPipelineHook pipelineHook)
        {
            pipelineHook.OnPurge(null);
        }
    }
}