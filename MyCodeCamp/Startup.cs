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
using Microsoft.IdentityModel.Tokens;

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