using System.Text;

namespace FeedHorn.Middleware;

public class FooterInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _footerHtml;
    private readonly string _protectionScript;

    public FooterInjectionMiddleware(RequestDelegate next)
    {
        _next = next;

        // Generate footer HTML with current year - this is compiled into the DLL
        var year = DateTime.Now.Year;
        _footerHtml = $@"
    <footer id=""app-footer"" class=""app-footer"">
        <span>&copy; {year} Powered by <a href=""https://zero16sec.com"" target=""_blank"" rel=""noopener noreferrer"" id=""footer-link"">Zero One Six Security, LLC</a></span>
    </footer>";

        // Protection script - minified and obfuscated
        _protectionScript = @"
    <script>
        (function(){var f=document.getElementById('app-footer');var l=document.getElementById('footer-link');if(!f||!l){var nf=document.createElement('footer');nf.id='app-footer';nf.className='app-footer';nf.innerHTML='© '+new Date().getFullYear()+' Powered by <a href=""https://zero16sec.com"" target=""_blank"" rel=""noopener noreferrer"" id=""footer-link"">Zero One Six Security, LLC</a>';document.body.appendChild(nf);}var o=new MutationObserver(function(m){m.forEach(function(r){if(r.type==='childList'||r.type==='attributes'){var ft=document.getElementById('app-footer');var lk=document.getElementById('footer-link');if(!ft||ft.style.display==='none'||ft.style.visibility==='hidden'||!lk||lk.getAttribute('href')!=='https://zero16sec.com'||lk.getAttribute('target')!=='_blank'){o.disconnect();location.reload();}}});});o.observe(document.body,{childList:true,subtree:true,attributes:true,characterData:true});if(f){o.observe(f,{childList:true,subtree:true,attributes:true,attributeOldValue:true});}setInterval(function(){var ft=document.getElementById('app-footer');if(!ft||ft.style.display==='none'||ft.style.visibility==='hidden'){location.reload();}},5000);})();
    </script>";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only intercept HTML responses
        var originalBodyStream = context.Response.Body;

        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        // Only inject footer for HTML content
        if (context.Response.ContentType != null &&
            context.Response.ContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

            // Inject footer before closing body tag
            if (body.Contains("</body>", StringComparison.OrdinalIgnoreCase))
            {
                body = body.Replace("</body>", _footerHtml + _protectionScript + "\n</body>", StringComparison.OrdinalIgnoreCase);

                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.Body = originalBodyStream;
                context.Response.ContentLength = bytes.Length;
                await context.Response.Body.WriteAsync(bytes);
                return;
            }
        }

        // If not HTML or no body tag, just copy the response as-is
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(originalBodyStream);
    }
}
