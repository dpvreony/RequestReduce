﻿using System;
using System.Collections.Specialized;
using System.Web;
using Moq;
using RequestReduce.Configuration;
using RequestReduce.Module;
using StructureMap;
using Xunit;
using System.IO;

namespace RequestReduce.Facts.Module
{
    public class RequestReduceModuleFacts
    {

        [Fact]
        public void WillSetResponseFilterOnce()
        {
            var module = new RequestReduceModule();
            var context = new Mock<HttpContextBase>();
            context.Setup(x => x.Items.Contains(RequestReduceModule.CONTEXT_KEY)).Returns(true);

            module.InstallFilter(context.Object);

            context.VerifySet((x => x.Response.Filter = It.IsAny<Stream>()), Times.Never());
        }

        [Fact]
        public void WillSetResponseFilterIfHtmlContent()
        {
            var module = new RequestReduceModule();
            var context = new Mock<HttpContextBase>();
            context.Setup(x => x.Items.Contains(RequestReduceModule.CONTEXT_KEY)).Returns(false);
            context.Setup(x => x.Response.ContentType).Returns("text/html");
            context.Setup(x => x.Request.QueryString).Returns(new NameValueCollection());
            context.Setup(x => x.Server).Returns(new Mock<HttpServerUtilityBase>().Object);

            module.InstallFilter(context.Object);

            context.VerifySet(x => x.Response.Filter = It.IsAny<Stream>(), Times.Once());
        }

        [Fact]
        public void WillSetPhysicalPathToMappedVirtualPath()
        {
            var module = new RequestReduceModule();
            var context = new Mock<HttpContextBase>();
            var config = new Mock<IRRConfiguration>();
            config.Setup(x => x.SpriteVirtualPath).Returns("/Virtual");
            context.Setup(x => x.Items.Contains(RequestReduceModule.CONTEXT_KEY)).Returns(false);
            context.Setup(x => x.Response.ContentType).Returns("text/html");
            context.Setup(x => x.Server.MapPath("/Virtual")).Returns("physical");
            context.Setup(x => x.Request.QueryString).Returns(new NameValueCollection());
            RRContainer.Current = new Container(x =>
                                                    {
                                                        x.For<IRRConfiguration>().Use(config.Object);
                                                        x.For<AbstractFilter>().Use(new Mock<AbstractFilter>().Object);
                                                    });

            module.InstallFilter(context.Object);

            config.VerifySet(x => x.SpritePhysicalPath = "physical", Times.Once());
            RRContainer.Current = null;
        }

        [Fact]
        public void WillNotSetResponseFilterIfRRFilterQSIsDisabled()
        {
            var module = new RequestReduceModule();
            var context = new Mock<HttpContextBase>();
            context.Setup(x => x.Items.Contains(RequestReduceModule.CONTEXT_KEY)).Returns(false);
            context.Setup(x => x.Response.ContentType).Returns("text/html");
            context.Setup(x => x.Request.QueryString).Returns(new NameValueCollection() {{"RRFilter", "disabled"}});
            context.Setup(x => x.Server).Returns(new Mock<HttpServerUtilityBase>().Object);

            module.InstallFilter(context.Object);

            context.VerifySet(x => x.Response.Filter = It.IsAny<Stream>(), Times.Never());
        }

        [Fact]
        public void WillSetContextKeyIfNotSetBefore()
        {
            var module = new RequestReduceModule();
            var context = new Mock<HttpContextBase>();
            context.Setup(x => x.Items.Contains(RequestReduceModule.CONTEXT_KEY)).Returns(false);
            context.Setup(x => x.Response.ContentType).Returns("type");
            context.Setup(x => x.Request.QueryString).Returns(new NameValueCollection());
            context.Setup(x => x.Server).Returns(new Mock<HttpServerUtilityBase>().Object);

            module.InstallFilter(context.Object);

            context.Verify(x => x.Items.Add(RequestReduceModule.CONTEXT_KEY, It.IsAny<Object>()), Times.Once());
        }

        [Fact]
        public void WillSetCachabilityIfInRRPathOnRelativeVirtualRRPath()
        {
            var module = new RequestReduceModule();
            var context = new Mock<HttpContextBase>();
            context.Setup(x => x.Request.RawUrl).Returns("/RRContent/css.css");
            context.Setup(x => x.Response.Headers).Returns(new NameValueCollection(){{"ETag", "tag"}});
            var cache = new Mock<HttpCachePolicyBase>();
            context.Setup(x => x.Response.Cache).Returns(cache.Object);
            var config = new Mock<IRRConfiguration>();
            config.Setup(x => x.SpriteVirtualPath).Returns("/RRContent");
            RRContainer.Current = new Container(x => x.For<IRRConfiguration>().Use(config.Object));

            module.SetCacheHeaders(context.Object);

            Assert.Equal(44000, context.Object.Response.Expires);
            Assert.Null(context.Object.Response.Headers["ETag"]);
            cache.Verify(x => x.SetCacheability(HttpCacheability.Public), Times.Once());
            RRContainer.Current = null;
        }

        [Fact]
        public void WillSetCachabilityIfInRRPathOnAbsoluteVirtualRRPath()
        {
            var module = new RequestReduceModule();
            var context = new Mock<HttpContextBase>();
            context.Setup(x => x.Request.RawUrl).Returns("/RRContent/css.css");
            context.Setup(x => x.Request.Url).Returns(new Uri("http://localhost/RRContent/css.css"));
            var headers = new NameValueCollection() {{"ETag", "tag"}};
            context.Setup(x => x.Response.Headers).Returns(headers);
            var cache = new Mock<HttpCachePolicyBase>();
            context.Setup(x => x.Response.Cache).Returns(cache.Object);
            var config = new Mock<IRRConfiguration>();
            config.Setup(x => x.SpriteVirtualPath).Returns("http://localhost/RRContent");
            RRContainer.Current = new Container(x => x.For<IRRConfiguration>().Use(config.Object));

            module.SetCacheHeaders(context.Object);

            Assert.Equal(44000, context.Object.Response.Expires);
            Assert.Null(headers["ETag"]);
            cache.Verify(x => x.SetCacheability(HttpCacheability.Public), Times.Once());
            RRContainer.Current = null;
        }

        [Fact]
        public void WillNotSetCachabilityIfNotInRRPathOnRelativeVirtualRRPath()
        {
            var module = new RequestReduceModule();
            var context = new Mock<HttpContextBase>();
            context.Setup(x => x.Request.RawUrl).Returns("/RRContent/css.css");
            context.Setup(x => x.Response.Headers).Returns(new NameValueCollection() { { "ETag", "tag" } });
            var cache = new Mock<HttpCachePolicyBase>();
            context.Setup(x => x.Response.Cache).Returns(cache.Object);
            var config = new Mock<IRRConfiguration>();
            config.Setup(x => x.SpriteVirtualPath).Returns("/Content");
            RRContainer.Current = new Container(x => x.For<IRRConfiguration>().Use(config.Object));

            module.SetCacheHeaders(context.Object);

            Assert.NotNull(context.Object.Response.Headers["ETag"]);
            cache.Verify(x => x.SetCacheability(HttpCacheability.Public), Times.Never());
            RRContainer.Current = null;
        }

        [Fact]
        public void WillNotSetCachabilityIfNotInRRPathOnAbsoluteVirtualRRPath()
        {
            var module = new RequestReduceModule();
            var context = new Mock<HttpContextBase>();
            context.Setup(x => x.Request.RawUrl).Returns("/RRContent/css.css");
            context.Setup(x => x.Request.Url).Returns(new Uri("http://localhost/RRContent/css.css"));
            context.Setup(x => x.Response.Headers).Returns(new NameValueCollection() { { "ETag", "tag" } });
            var cache = new Mock<HttpCachePolicyBase>();
            context.Setup(x => x.Response.Cache).Returns(cache.Object);
            var config = new Mock<IRRConfiguration>();
            config.Setup(x => x.SpriteVirtualPath).Returns("http://localhost/Content");
            RRContainer.Current = new Container(x => x.For<IRRConfiguration>().Use(config.Object));

            module.SetCacheHeaders(context.Object);

            Assert.NotNull(context.Object.Response.Headers["ETag"]);
            cache.Verify(x => x.SetCacheability(HttpCacheability.Public), Times.Never());
            RRContainer.Current = null;
        }

        [Fact]
        public void WillNotSetPhysicalPathToMappedPathOfVirtualPathIfPhysicalPathIsNotEmpty()
        {
            var module = new RequestReduceModule();
            var context = new Mock<HttpContextBase>();
            var config = new Mock<IRRConfiguration>();
            config.Setup(x => x.SpritePhysicalPath).Returns("physicalPath");
            RRContainer.Current = new Container(x =>
            {
                x.For<IRRConfiguration>().Use(config.Object);
                x.For<AbstractFilter>().Use(new Mock<AbstractFilter>().Object);
            });
            context.Setup(x => x.Items.Contains(RequestReduceModule.CONTEXT_KEY)).Returns(false);
            context.Setup(x => x.Request.QueryString).Returns(new NameValueCollection());
            context.Setup(x => x.Response.ContentType).Returns("text/html");

            module.InstallFilter(context.Object);

            config.VerifySet(x => x.SpritePhysicalPath = It.IsAny<string>(), Times.Never());
            RRContainer.Current = null;
        }

    }
} 