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

        // Protection script - obfuscated
        _protectionScript = @"
    <script>
        (function(){const _0x5b3a=['year','getElementById','app-footer','footer-link','getFullYear','https://zero16sec.com','Zero\x20One\x20Six\x20Security,\x20LLC','setAttribute','href','target','_blank','textContent','©\x20','Powered\x20by\x20','observe','childList','subtree','attributes','characterData','disconnect','MutationObserver','body','appendChild','style','display','position','bottom','visibility','none','hidden'];(function(_0x2d4e5f,_0x5b3a71){const _0x4c6d19=function(_0x3e7f47){while(--_0x3e7f47){_0x2d4e5f['push'](_0x2d4e5f['shift']());}};_0x4c6d19(++_0x5b3a71);}(_0x5b3a,0x14d));const _0x4c6d=function(_0x2d4e5f,_0x5b3a71){_0x2d4e5f=_0x2d4e5f-0x0;let _0x4c6d19=_0x5b3a[_0x2d4e5f];return _0x4c6d19;};const ft=document[_0x4c6d('0x1')](_0x4c6d('0x2'));const lk=document[_0x4c6d('0x1')](_0x4c6d('0x3'));if(!ft||!lk){const f=document['createElement']('footer');f['id']=_0x4c6d('0x2');f['className']='app-footer';f['innerHTML']=_0x4c6d('0xc')+new Date()[_0x4c6d('0x4')]()+'\x20'+_0x4c6d('0xd')+'<a\x20href=\x22'+_0x4c6d('0x5')+'\x22\x20target=\x22_blank\x22\x20rel=\x22noopener\x20noreferrer\x22\x20id=\x22footer-link\x22>'+_0x4c6d('0x6')+'</a>';document[_0x4c6d('0x15')][_0x4c6d('0x16')](f);}const ob=new MutationObserver(function(_0x3e7f47){_0x3e7f47['forEach'](function(_0x5a8c2b){if(_0x5a8c2b['type']===_0x4c6d('0x10')||_0x5a8c2b['type']===_0x4c6d('0x12')){const _0x2b9d7c=document[_0x4c6d('0x1')](_0x4c6d('0x2'));const _0x6d4f8e=document[_0x4c6d('0x1')](_0x4c6d('0x3'));if(!_0x2b9d7c||_0x2b9d7c[_0x4c6d('0x17')][_0x4c6d('0x18')]===_0x4c6d('0x1c')||_0x2b9d7c[_0x4c6d('0x17')][_0x4c6d('0x1a')]===_0x4c6d('0x1b')||!_0x6d4f8e||_0x6d4f8e['getAttribute'](_0x4c6d('0x8'))!==_0x4c6d('0x5')||_0x6d4f8e['getAttribute'](_0x4c6d('0x9'))!==_0x4c6d('0xa')){ob[_0x4c6d('0x13')]();location['reload']();}}});});ob['observe'](document[_0x4c6d('0x15')],{childList:!![],subtree:!![],attributes:!![],characterData:!![]});if(ft){ob[_0x4c6d('0xe')](ft,{childList:!![],subtree:!![],attributes:!![],attributeOldValue:!![]});}setInterval(function(){const _0x4a5e2b=document[_0x4c6d('0x1')](_0x4c6d('0x2'));if(!_0x4a5e2b||_0x4a5e2b[_0x4c6d('0x17')][_0x4c6d('0x18')]===_0x4c6d('0x1c')||_0x4a5e2b[_0x4c6d('0x17')][_0x4c6d('0x1a')]===_0x4c6d('0x1b')){location['reload']();}},0x1388);})();
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
