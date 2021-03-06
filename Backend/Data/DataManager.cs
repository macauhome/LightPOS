﻿//
// Copyright (c) NickAc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using NHibernate;
using NickAc.LightPOS.Backend.Objects;
using NickAc.LightPOS.Backend.Utils;

namespace NickAc.LightPOS.Backend.Data
{
    public static class DataManager
    {
        #region Properties

        public static DataFactory DataFactory { get; set; }

        public static ISessionFactory SessionFactory { get; set; }

        #endregion

        #region Methods

        public static void AddCategory(Category c)
        {
            using (SessionFactory.OpenSessionWithTransaction(out var session))
            {
               session.SaveOrUpdate(c);
            }
        }

        public static void AddProduct(Product p)
        {
            using (SessionFactory.OpenSessionWithTransaction(out var session))
            {
                session.SaveOrUpdate(p.Category);
                session.SaveOrUpdate(p);
            }
        }

        public static void AddSale(Sale s)
        {
            using (SessionFactory.OpenSessionWithTransaction(out var session))
            {
                if (!NHibernateUtil.IsInitialized(s.Customer.Sales))
                    NHibernateUtil.Initialize(s.Customer.Sales);
                s.Customer.Sales.Add(s);
                session.SaveOrUpdate(s.User);
                session.SaveOrUpdate(s.Customer);
                session.SaveOrUpdate(s);
            }
        }

        public static void AddUser(User user)
        {
            using (SessionFactory.OpenSessionWithTransaction(out var session))
            {
                try
                {
                    if (!NHibernateUtil.IsInitialized(user.Actions))
                        NHibernateUtil.Initialize(user.Actions);
                    if (!NHibernateUtil.IsInitialized(user.Sales))
                        NHibernateUtil.Initialize(user.Sales);
                }
                catch (HibernateException)
                {
                }

                session.SaveOrUpdate(user);
            }
        }


        public static decimal CalculateTotal(IEnumerable<Product> products)
        {
            var total = 0.0m;
            products.All(p =>
            {
                total += p.CalculatePrice();
                return true;
            });
            return total;
        }

        public static Sale CreateSale(Customer customer, User user, decimal paidPrice, params Product[] prods)
        {
            var total = CalculateTotal(prods);
            var finalSale = new Sale
            {
                TotalPrice = total,
                PaidPrice = paidPrice,
                ChangePrice = paidPrice - total,
                Products = prods,
                Customer = customer,
                User = user
            };
            if (!NHibernateUtil.IsInitialized(user.Sales))
                NHibernateUtil.Initialize(user.Sales);
            user.Sales.Add(finalSale);
            return finalSale;
        }


        public static Customer GetCustomer(int id)
        {
            using (SessionFactory.OpenSession(out var session))
            {
                var customer = session.QueryOver<Customer>().Where(x => x.Id == id).List().FirstOrDefault();
                return customer;
            }
        }

        public static int GetNumberOfUsers()
        {
            int userNumber;
            using (SessionFactory.OpenSession(out var session))
            {
                userNumber = session.QueryOver<User>().ToRowCountQuery().FutureValue<int>().Value;
            }

            return userNumber;
        }

        public static IList<User> GetUsers()
        {
            IList<User> list;
            using (SessionFactory.OpenSession(out var session))
            {
                    list = session.QueryOver<User>().List();
            }

            return list;
        }


        public static IList<Category> GetCategories()
        {
            IList<Category> list;
            using (SessionFactory.OpenSession(out var session))
            {
                    list = session.QueryOver<Category>().List();
            }

            return list;
        }

        public static User GetUser(int id)
        {
            User user;
            using (SessionFactory.OpenSession(out var session))
            {
                    user = session.QueryOver<User>().Fetch(u => u.Actions)
                        .Eager.Where(u => u.UserId == id).List().FirstOrDefault();
            }

            return user;
        }

        public static IList<User> GetUsersWithSales()
        {
            IList<User> users;
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    users = session
                        .QueryOver<User>()
                        .Fetch(u => u.Sales).Eager
                        .List();
                }
            }

            return users;
        }


        public static User GetUserWithActions(int id)
        {
            User user;
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    user = session
                        .QueryOver<User>()
                        .Fetch(u => u.Actions).Eager
                        .Where(u => u.UserId == id)
                        .List()
                        .FirstOrDefault();
                }
            }

            return user;
        }


        public static User GetUserWithSales(int id)
        {
            User user;
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    user = session
                        .QueryOver<User>()
                        .Fetch(u => u.Sales).Eager
                        .Where(u => u.UserId == id)
                        .List()
                        .FirstOrDefault();
                }
            }

            return user;
        }


        public static IList<User> GetUsersWithActions()
        {
            IList<User> users;
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    users = session
                        .QueryOver<User>()
                        .Fetch(u => u.Actions).Eager
                        .List();
                }
            }

            return users;
        }

        public static void LogAction(User user, UserAction.Action eventAction, string info)
        {
            var freshUser = GetUser(user.UserId);
            var action = new UserAction
            {
                Time = DateTime.Now,
                Event = eventAction,
                Description = info
            };
            freshUser.Actions.Add(action);

            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    using (var trans = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(action);
                        if (!NHibernateUtil.IsInitialized(freshUser.Actions))
                            NHibernateUtil.Initialize(freshUser.Actions);
                        session.SaveOrUpdate(freshUser);
                        trans.Commit();
                    }
                }
            }
        }

        public static Product GetProduct(int id)
        {
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    var product = session.QueryOver<Product>()
                        .Fetch(p => p.Category).Eager
                        .Where(x => x.Id == id).List().FirstOrDefault();
                    return product;
                }
            }
        }

        public static Product GetProduct(string barcode)
        {
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    var product = session.QueryOver<Product>().Where(x => x.Barcode == barcode).List().FirstOrDefault();
                    return product;
                }
            }
        }

        public static IList<Product> GetProducts()
        {
            IList<Product> list;
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    list = session.QueryOver<Product>().Fetch(p => p.Category).Eager.List();
                }
            }

            return list;
        }

        public static void Initialize(FileInfo file)
        {
            TimeMeasurer.MeasureTime("new DataFactory();", () =>
            {
                if (DataFactory == null) DataFactory = new DataFactory(file, false);
            });
            TimeMeasurer.MeasureTime("DataFactory.Create();", () =>
            {
                DataFactory?.Create();
            });

            if (SessionFactory == null) SessionFactory = DataFactory.CreateSessionFactory();
        }

        /// <summary>
        ///     Never called! Exists just to tell Visual Studio to copy the assemblies to the build directory
        /// </summary>
        public static void InitStuff()
        {
            var cmd = new SQLiteCommand();
            cmd.Dispose();
        }

        public static void RemoveCategory(Category c)
        {
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    using (var trans = session.BeginTransaction())
                    {
                        session.Delete(c);
                        trans.Commit();
                    }
                }
            }
        }

        public static void RemoveProduct(Product p)
        {
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    using (var trans = session.BeginTransaction())
                    {
                        session.Delete(p);
                        trans.Commit();
                    }
                }
            }
        }


        public static void RemoveUser(User u)
        {
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    using (var trans = session.BeginTransaction())
                    {
                        session.Delete(u);
                        trans.Commit();
                    }
                }
            }
        }

        public static void RemoveUserSales(User u)
        {
            if (u.Sales.Count == 0) return;
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    using (var trans = session.BeginTransaction())
                    {
                        u.Sales.All(x =>
                        {
                            session.Delete(x);
                            return true;
                        });
                        trans.Commit();
                    }
                }
            }
        }

        public static void RemoveUserActions(User u)
        {
            if (u.Actions.Count == 0) return;
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    using (var trans = session.BeginTransaction())
                    {
                        u.Actions.All(x =>
                        {
                            session.Delete(x);
                            return true;
                        });
                        trans.Commit();
                    }
                }
            }
        }


        public static void RemoveUser(int userId)
        {
            var finalUser1 = GetUserWithActions(userId);
            RemoveUser(finalUser1);
        }

        public static void RemoveProduct(int productId)
        {
            using (var sf = SessionFactory)
            {
                using (var session = sf.OpenSession())
                {
                    using (var trans = session.BeginTransaction())
                    {
                        session.Delete("from Product p where p.ID = ?", productId, NHibernateUtil.Int32);
                        trans.Commit();
                    }
                }
            }
        }

        #endregion
    }
}