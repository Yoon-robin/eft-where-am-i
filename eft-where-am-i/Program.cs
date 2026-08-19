using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using eft_where_am_i.Classes;
using Velopack;

namespace eft_where_am_i_chasrp
{
    internal static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            VelopackApp.Build().Run();

            // 구버전이 %TEMP% 에 남긴 WebView2 프로필을 백그라운드에서 정리합니다.
            Task.Run(WebViewEnvironmentFactory.CleanupStaleProfiles);

            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
