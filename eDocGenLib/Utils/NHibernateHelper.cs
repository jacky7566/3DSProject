using NHibernate;
using NHibernate.Cfg;
using NHibernate.Persister.Entity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;

namespace eDocGenLib.Utils
{
    public class NHibernateHelper
    {
        private static ISessionFactory _sessionFactory;

        private static ISessionFactory SessionFactory
        {
            get
            {
                if (_sessionFactory == null)
                {
                    //var cfg = new Configuration();
                    //cfg.Configure();
                    _sessionFactory = Configuration.BuildSessionFactory();
                }
                return _sessionFactory;
            }
        }

        private static NHibernate.Cfg.Configuration _cfg;

        private static NHibernate.Cfg.Configuration Configuration
        {
            get
            {
                if (_cfg == null)
                {
                    string conStrName = System.Configuration.ConfigurationManager.AppSettings["DBName"].ToString();
                    _cfg = new Configuration();

                    _cfg.SetProperty(
                      NHibernate.Cfg.Environment.ConnectionDriver,
                      typeof(NHibernate.Driver.SqlClientDriver).AssemblyQualifiedName);

                    _cfg.SetProperty(
                      NHibernate.Cfg.Environment.Dialect,
                      typeof(NHibernate.Dialect.MsSql2008Dialect).AssemblyQualifiedName);

                    //_cfg.SetProperty(NHibernate.Cfg.Environment.ConnectionString, conStr);
                    _cfg.SetProperty(NHibernate.Cfg.Environment.ConnectionStringName, conStrName);
                    //_cfg.SetProperty(NHibernate.Cfg.Environment.FormatSql, "true");
                    _cfg.SetProperty(NHibernate.Cfg.Environment.ShowSql, "false");
                    _cfg.SetProperty(NHibernate.Cfg.Environment.BatchSize, "100");
                    _cfg.SetProperty(NHibernate.Cfg.Environment.UseQueryCache, "false");
                    _cfg.SetProperty(NHibernate.Cfg.Environment.CommandTimeout, "600");

                    _cfg.AddAssembly(Assembly.GetExecutingAssembly());
                }
                return _cfg;
            }
        }

        public static ISession OpenSession()
        {
            return SessionFactory.OpenSession();
        }

        public static IStatelessSession OpenStatelessSession()
        {
            return SessionFactory.OpenStatelessSession();
        }

        public static string GetClassName(string tableName)
        {
            //return Configuration.ClassMappings.Where(x => x.Table.Name == tableName).First().EntityName;
            string tbNameUpper = tableName.ToUpper();
            var allClassData = SessionFactory.GetAllClassMetadata();
            var persisters = allClassData.Where(p => ((AbstractEntityPersister)p.Value).TableName.ToUpper() == tbNameUpper);

            return persisters.FirstOrDefault().Value.EntityName;
        }

        public static string[] GetAllClassName()
        {
            return SessionFactory.GetAllClassMetadata().Select(p => p.Value.EntityName).ToArray();
        }

        public static string GetTableName(string classFullName)
        {
            //return Configuration.ClassMappings.Where(x => x.EntityName == classFullName).First().Table.Name;
            var persister = (AbstractEntityPersister)SessionFactory.GetClassMetadata(classFullName);
            return persister.TableName;
        }

        public static string GetColumnName(string classFullName, string propertyName)
        {
            var persister = (AbstractEntityPersister)SessionFactory.GetClassMetadata(classFullName);
            //var propertyNameList = persister.PropertyNames;

            return persister.GetPropertyColumnNames(propertyName).First();
        }

        public static List<string> GetColumnNames(Type type)
        {
            var namespaceValue = type.Namespace;
            switch (namespaceValue)
            {
                case "SystemLibrary.Models":
                    string classFullName = type.FullName;

                    var persister = (AbstractEntityPersister)SessionFactory.GetClassMetadata(classFullName);
                    List<string> columnNames = persister.KeyColumnNames.ToList();
                    foreach (string propertyName in persister.PropertyNames)
                    {
                        columnNames.Add(persister.GetPropertyColumnNames(propertyName).First());
                    }

                    return columnNames;                    
                default:
                    return type.GetProperties().Select(x => (x.GetCustomAttributes(typeof(DisplayAttribute), true).FirstOrDefault() as DisplayAttribute).Name).ToList();                    
            }            
        }

        public static string GetPropertyName(Type type, string columnName)
        {
            var namespaceValue = type.Namespace;

            switch (namespaceValue)
            {
                case "SystemLibrary.Models":
                    string propertyName = "";
                    string className = type.FullName;
                    var propertyNames = type.GetProperties().Select(p => p.Name);
                    var persister = (AbstractEntityPersister)SessionFactory.GetClassMetadata(className);
                    foreach (var tmpPropertyName in propertyNames)
                    {
                        string tempName = persister.GetPropertyColumnNames(tmpPropertyName).First();
                        if (tempName.ToUpper() == columnName.ToUpper())
                        {
                            propertyName = tmpPropertyName;
                            break;
                        }
                    }
                    return propertyName;
                default:
                    return type.GetProperties()
                        .Where(x => (x.GetCustomAttributes(typeof(DisplayAttribute), true).FirstOrDefault() as DisplayAttribute).Name == columnName)
                        .FirstOrDefault().Name;
            }
        }
    }
}