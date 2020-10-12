using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SFTPTransfer
{
    class Program
    {
        static void Main(string[] args)
        {
            IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile("config.json").Build();
            string logFile = config.GetSection("LogFilePath").Value.Replace("[yyyymmdd]", DateTime.Now.ToString("yyyMMdd"));
            //Log log = new Log(logFile);
            string env = config.GetSection("Environment").Value;
            Console.WriteLine($"Vendor is {env}");

            try
            {
                Processor processor = new Processor(config, env);

                processor.ConnectSFTPSite(args);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
