using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyCodeCamp.Data;
using MyCodeCamp.Data.Entities;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Mvc.Versioning.Conventions;
using Microsoft.IdentityModel.Tokens;
using MyCodeCamp.Controllers;
using MyCodeCamp.Models;

namespace MyCodeCamp
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            _env = env;
            _config = builder.Build();
        }

        IConfigurationRoot _config;
        IHostingEnvironment _env;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_config);
            services.AddDbContext<CampContext>(ServiceLifetime.Scoped);
            services.AddScoped<ICampRepository, CampRepository>();
            services.AddTransient<CampDbInitializer>();
            services.AddTransient<CampIdentityInitializer>();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddAutoMapper();

            services.AddMemoryCache();

            services.AddIdentity<CampUser, IdentityRole>()
                .AddEntityFrameworkStores<CampContext>(); // Context that contains the identity information
            services.Configure<IdentityOptions>(config =>
            {
                config.Cookies.ApplicationCookie.Events =
                    new CookieAuthenticationEvents()
                    {
                        // override default behavior that redirects unauthorized requests to login page
                        OnRedirectToLogin = (ctx) =>
                        {
                            // Do not redirect to login page if this is existing API call
                            if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Response.StatusCode == 200)
                            {
                                ctx.Response.StatusCode = 401;
                            }
                            return Task.CompletedTask;
                        },
                        OnRedirectToAccessDenied = (ctx) =>
                        {
                            if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Response.StatusCode == 200)
                            {
                                ctx.Response.StatusCode = 401;
                            }
                            return Task.CompletedTask;
                        }
                    };
            });


            /*
             * Versioning
             * 
             * URI Path
             * https://foo.org/api/v2/customers
             * 
             * Query String (are optional)
             * https://foo.org/api/v2/customers?v=2.0
             * 
             * Headers
             * X-Version: 2.0
             * 
             * Accept Jeader
             * Accept: application/json:version=2.0
             * 
             * Content Type Header
             * Content-Type: application/vnd.yourapp.camp.v1+json
             */
            // After adding this to services, all calls by default require the foollowing query parameter:
            // ?api-version=1.0
            services.AddApiVersioning(config =>
            {
                // set default api-version to 1.1 (api-version=1.1)
                config.DefaultApiVersion = new ApiVersion(1, 1); // Sets default api version to 1.1
                config.AssumeDefaultVersionWhenUnspecified = true; // apply default version if none specified
                config.ReportApiVersions = true; // Send the header that will tell about the available versions
                // Configure API versioning methods (how api version is read and mapped to correct controller action
                config.ApiVersionReader =
                    new HeaderApiVersionReader("ver",
                        "X-MyCodeCamp-Version"); // Header 'ver : 2.0' or 'X-MyCodeCamp-Version : 2.0'
                // Default is Query
                /* Also supported;
                 * QueryStringOrHeaderApiVersionReader("ver")
                 */

                // Set up configuration conventions ( to avoid using annotation attributes)
                // More info https://github.com/Microsoft/aspnet-api-versioning/wiki/API-Version-Conventions
                config.Conventions.Controller<TalksController>()
                    .HasApiVersion(new ApiVersion(1, 0))
                    .HasApiVersion(new ApiVersion(1, 1))
                    .HasApiVersion(new ApiVersion(2, 0))
                    .Action(talksController => talksController.Post(default(string), default(int), default(TalkModel)))
                    .MapToApiVersion(new ApiVersion(2, 0));
            });

            services.AddAuthorization(config =>
            {
                config.AddPolicy("SuperUsers", policy => policy.RequireClaim("SuperUser", "True")); // Claim
            });

            // Add framework services.
            // Add support to SSL by passing configuration lambda expression
            // which adds Https requirement to global filters
            services.AddMvc(options =>
                {
                    if (!_env.IsProduction())
                    {
                        // If we are not in a production, then add HTTPS port explicitly to ASP redirects
                        // Request to http://localhost:8088/api/camps will contain header:
                        // Location →https://localhost/api/camps
                        // Which doesn't have SSL Port added by default

                        options.SslPort = 44388;
                        //Location →https://localhost:44388/api/camps
                    }

                    // Looks and whether request comes on HTTP or HTTPS
                    // If it is HTTP, it will redirect to HTTPS using the same request
                    //TODO FIXME Don't know why, but POST requests because of it!!!
                    //options.Filters.Add(new RequireHttpsAttribute());
                })
                .AddJsonOptions(opt =>
                {
                    opt.SerializerSettings.ReferenceLoopHandling =
                        ReferenceLoopHandling.Ignore;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            CampDbInitializer seeder,
            CampIdentityInitializer identitySeeder)
        {
            loggerFactory.AddConsole(_config.GetSection("Logging"));
            loggerFactory.AddDebug();

            // Make Identity protect calls
            // By default unauthorized requests will be redirected to /Accounts/Login with a Return URL Query parameters
            // Trick to avoid: add header: X-Requested-With : XMLHttpRequest to get 401 code (simulate a call like from javascript)
            app.UseIdentity();

            app.UseJwtBearerAuthentication(new JwtBearerOptions()
            {
                AutomaticAuthenticate = true,
                AutomaticChallenge = true,
                TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidIssuer = _config["Tokens:Issuer"],
                    ValidAudience = _config["Tokens:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Tokens:Key"])),
                    ValidateLifetime = true // Expiration date is not expired
                }
            });

            app.UseMvc(config =>
            {
                //config.MapRoute("MainAPIRoute", "api/{controller}/{action}");
            });

            seeder.Seed().Wait();
            identitySeeder.Seed().Wait();
        }
    }
}