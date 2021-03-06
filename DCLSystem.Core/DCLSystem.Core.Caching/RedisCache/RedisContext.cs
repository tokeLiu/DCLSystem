﻿using System.Collections.Concurrent;
using System.Globalization;
using DCLSystem.Core.Caching.DependencyResolution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DCLSystem.Core.Caching.HashAlgorithms;

namespace DCLSystem.Core.Caching.RedisCache
{

    /// <summary>
    /// redis数据上下文
    /// </summary>
    /// <remarks>
    /// 	<para>创建：范亮</para>
    /// 	<para>日期：2016/4/2</para>
    /// </remarks>
    public class RedisContext
    {
        /// <summary>
        /// 缓存对象集合容器池
        /// </summary>
        /// <remarks>
        /// 	<para>创建：范亮</para>
        /// 	<para>日期：2016/4/2</para>
        /// </remarks>
        internal Lazy<Dictionary<string, List<string>>> _cachingContextPool;

        /// <summary>
        /// 密码
        /// </summary>
        /// <remarks>
        /// 	<para>创建：范亮</para>
        /// 	<para>日期：2016/4/2</para>
        /// </remarks>
        internal string _password;

        internal string _bucket;
        /// <summary>
        /// 默认缓存失效时间
        /// </summary>
        /// <remarks>
        /// 	<para>创建：范亮</para>
        /// 	<para>日期：2016/4/2</para>
        /// </remarks>
        internal string _defaultExpireTime;

        /// <summary>
        /// 连接失效时间
        /// </summary>
        /// <remarks>
        /// 	<para>创建：范亮</para>
        /// 	<para>日期：2016/4/2</para>
        /// </remarks>
        internal string _connectTimeout;

        /// <summary>
        /// 规则名（现在只实现哈希一致性）
        /// </summary>
        /// <remarks>
        /// 	<para>创建：范亮</para>
        /// 	<para>日期：2016/4/2</para>
        /// </remarks>
        internal string _ruleName;

        /// <summary>
        /// 哈希节点容器
        /// </summary>
        /// <remarks>
        /// 	<para>创建：范亮</para>
        /// 	<para>日期：2016/4/2</para>
        /// </remarks>
        internal ConcurrentDictionary<string, ConsistentHash<ConsistentHashNode>> dicHash;

        /// <summary>
        /// 对象池上限
        /// </summary>
        internal string _maxSize;

        /// <summary>
        /// 对象池下限
        /// </summary>
        internal string _minSize;

        #region 构造函数
        /// <summary>
        /// redis数据上下文
        /// </summary>
        /// <param name="rule">规则</param>
        /// <param name="args">参数</param>
        /// <remarks>
        /// 	<para>创建：范亮</para>
        /// 	<para>日期：2016/4/2</para>
        /// </remarks>
        public RedisContext(string rule, params object[] args)
        {
            foreach (var arg in args)
            {
                var properties = arg.GetType().GetProperties();
                var field = this.GetType()
                    .GetField(string.Format("_{0}", properties[0].GetValue(arg).ToString()),
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (properties.Count() == 3)
                {
                    _cachingContextPool = new Lazy<Dictionary<string, List<string>>>(
                        () =>
                        {
                            var dataContextPool = new Dictionary<string, List<string>>();
                            var lArg = arg as List<object>;
                            foreach (var tmpArg in lArg)
                            {
                                var props = tmpArg.GetType().GetProperties();
                                var items = props[2].GetValue(tmpArg) as object[];
                                var list = (from item in items
                                    let itemProperties = item.GetType().GetProperties()
                                    select itemProperties[1].GetValue(item)
                                    into value
                                    select value.ToString()).ToList();

                                dataContextPool.Add(props[1].GetValue(tmpArg).ToString(), list);
                            }
                            return dataContextPool;
                        }
                        );
                }
                else
                {
                    if (field != null) field.SetValue(this, properties[1].GetValue(arg));
                }
            }
            _ruleName = rule;
            dicHash = new ConcurrentDictionary<string, ConsistentHash<ConsistentHashNode>>();
            InitSettingHashStorage();
        }
        #endregion

        #region 属性
        /// <summary>
        /// 缓存对象集合容器池
        /// </summary>
        /// <remarks>
        /// 	<para>创建：范亮</para>
        /// 	<para>日期：2016/4/2</para>
        /// </remarks>
        public Dictionary<string, List<string>> DataContextPool
        {
            get { return _cachingContextPool.Value; }
        }
        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化设置哈希节点容器
        /// </summary>
        /// <remarks>
        /// 	<para>创建：范亮</para>
        /// 	<para>日期：2016/4/2</para>
        /// </remarks>
        private void InitSettingHashStorage()
        {
            foreach (var dataContext in DataContextPool)
            {
                CacheTargetType targetType;
                if (!Enum.TryParse(dataContext.Key, true, out targetType)) continue;
                var hash =
                    new ConsistentHash<ConsistentHashNode>(ServiceResolver.Current.GetService<IHashAlgorithm>());

                dataContext.Value.ForEach(v =>
                {
                    var db ="";
                    var dbs = v.Split(new string[] {"::"}, StringSplitOptions.RemoveEmptyEntries);
                    var server = v.Split('@');
                    var endpoints = server.Length > 1 ? server[1].Split(':') : server[0].Split(':');
                    var account =server.Length > 1 ? server[0].Split(':'): null;
                    var username = account != null && account.Length > 1 ? account[0] : null;
                    var password = server.Length > 1 ? account[account.Length - 1] : this._password;
                    if (endpoints.Length <= 1) return;
                    if (dbs.Length > 1)
                    {
                        db = dbs[dbs.Length - 1];
                    }
                    hash.Add(new ConsistentHashNode()
                    {
                        Type = targetType,
                        Host = endpoints[0],
                        Port = endpoints[1],
                        UserName = username,
                        Password = password,
                        MaxSize = this._maxSize,
                        MinSize = this._minSize,
                        Db = db.ToString(CultureInfo.InvariantCulture)
                    });
                    dicHash.GetOrAdd(targetType.ToString(), hash);
                });
            }
        }

        #endregion
    }
}