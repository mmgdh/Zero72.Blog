namespace Zero72.Blog.Deployer.Models;

/// <summary>
/// 描述一次成功部署后需要展示给用户的结果。
/// </summary>
public sealed record DeploymentResult(string ReleaseId, Uri BlogUrl, Uri AdminUrl);
