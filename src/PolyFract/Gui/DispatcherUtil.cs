using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using PolyFract.Maths;

namespace PolyFract.Gui
{
    public static class DispatcherUtil
    {
        private static bool uiPending;
        public static void DispatchToUi(DispatcherPriority priority, Action action)
        {
            if (System.Windows.Application.Current?.Dispatcher != null && !uiPending)
            {
                uiPending = true;
                try
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    priority,
                    (Action)(() =>
                    {

                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                        finally
                        {
                            uiPending = false;
                        }
                    }));
                }
                catch (Exception ex)
                {

                }
            }
        }
    }
}
