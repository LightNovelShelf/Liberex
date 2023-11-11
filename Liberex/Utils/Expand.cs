using Microsoft.AspNetCore.Mvc;

namespace Liberex.Utils;

public static class Expand
{
    public async static Task Try(this Task task)
    {
        try
        {
            await task;
        }
        catch (Exception)
        {

        }
    }

    public static Task ExecuteResultAsync(this ControllerBase controller, ActionResult result)
    {
        return result.ExecuteResultAsync(new ActionContext(controller.HttpContext, controller.RouteData, controller.ControllerContext.ActionDescriptor, controller.ModelState));
    }
}