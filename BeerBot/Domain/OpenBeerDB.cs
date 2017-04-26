﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using MySql.Data.MySqlClient;

namespace Domain
{
    public class OpenBeerDB
        // Manipulations of the database will use this class
    {
        private string mySqlConnectionString = "SERVER=127.0.0.1; DATABASE=openbeerdb; UID=root; PASSWORD=";
        private MySqlConnection connection;

        public List<User> users { get; private set; }
        public List<Beer> beers { get; private set; }
        public List<Category> categories { get; private set; }
        private List<double[]> ratings { get; set; } // double[3] : { userid, beerid, rating }

        public OpenBeerDB()
        {
            connection = new MySqlConnection(mySqlConnectionString);
            generateFactFile(); // creating the file "facts.pl", countaining all the facts from the database in their prolog form
            ratings = getRatings();
            users = getUsers();
            categories = getCategories();
            beers = getBeers();
        }

        public void UpdateDatabase()
            // to call when data is added / deleted, to keep the application up to date with it's data
        {
            generateFactFile(); // creating the file "facts.pl", countaining all the facts from the database in their prolog form
            ratings = getRatings();
            users = getUsers();
            categories = getCategories();
            beers = getBeers();
        }

        public User UpdateUser(int userId)
        {
            foreach (User user in users)
            {
                if (user.id == userId)
                    return user;
            }
            return new User("ERROR", "ERROR", 0000, false); // Error value
        }

        private List<User> getUsers()
        {
            List<string[]> usersAttributes = Select(new string[] { "id", "name", "password", "birth_year", "gender", "admin" }, "users");
            List<User> users = new List<User> { };
            foreach(string[] userAttributes in usersAttributes)
            {
                List<double[]> userRatings = new List<double[]> { };
                foreach (double[] rating in ratings)
                {
                    if (rating[0] == int.Parse(userAttributes[0]))
                    {
                        userRatings.Add(new double[] { rating[1], rating[2] });
                    }
                }
                users.Add(new User(
                    int.Parse(userAttributes[0]),
                    userAttributes[1],
                    userAttributes[2],
                    int.Parse(userAttributes[3]),
                    bool.Parse(userAttributes[4]),
                    userRatings,
                    bool.Parse(userAttributes[5])));
            }
            return users;
        }

        private List<Beer> getBeers()
        {
            List<string[]> beersAttributes = Select(new string[]
            {
                "id", "name", "cat_id", "style_id", "abv", "ibu", "srm", "descript"
            }, "beers");
            List<Beer> beers = new List<Beer> { };
            foreach (string[] beerAttributes in beersAttributes)
            {
                if (int.Parse(beerAttributes[0]) != -1) // if the beer isn't the default beer
                {
                    Style sty = new Style("Unknown Style", -1);
                    Category cat = new Category("Unknown Category", -1, new List<Style> { sty });
                    foreach (Category category in categories)
                    {
                        if (int.Parse(beerAttributes[2]) == category.id)
                            cat = category;
                        foreach (Style style in category.styles)
                        {
                            if (int.Parse(beerAttributes[3]) == style.id)
                                sty = style;
                        }
                    }

                    beers.Add(new Beer(
                        beerAttributes[1],
                        beerAttributes[7],
                        int.Parse(beerAttributes[0]),
                        cat,
                        sty,
                        double.Parse(beerAttributes[4]),
                        double.Parse(beerAttributes[5]),
                        double.Parse(beerAttributes[6])
                        ));
                }
            }
            return beers;
        }

        private List<Category> getCategories()
        {
            List<string[]> categoriesAttributes = Select(new string[]
            {
                "id", "cat_name"
            }, "categories");
            List<Category> categories = new List<Category> { };
            foreach (string[] categoryAttributes in categoriesAttributes)
            {
                #region getting styles in this category
                List<string[]> stylesAttributes = Select(new string[]
                {
                    "id", "cat_id", "style_name"
                }, "styles");
                List<Style> styles = new List<Style> { };
                foreach (string[] styleAttributes in stylesAttributes)
                {
                    if (int.Parse(styleAttributes[1]) == int.Parse(categoryAttributes[0]))
                        styles.Add(new Style(styleAttributes[2], int.Parse(styleAttributes[0])));
                }
                #endregion
                categories.Add(new Category(categoryAttributes[1], int.Parse(categoryAttributes[0]), styles));
            }
            return categories;
        }

        private List<double[]> getRatings()
        {
            List<string[]> ratingsAttributes = Select(new string[]
            {
                "user_id", "beer_id", "rating"
            }, "ratings");
            List<double[]> ratings = new List<double[]> { };
            foreach (string[] ratingAttributes in ratingsAttributes)
            {
                ratings.Add(new double[3]
                {
                    double.Parse(ratingAttributes[0]),
                    double.Parse(ratingAttributes[1]),
                    double.Parse(ratingAttributes[2])
                });
            }
            return ratings;
        }

        public List<Request> getRequests()
        {
            List<string[]> requestsAttributes = Select(new string[]
            {
                "id", "beer_id", "name", "cat_id", "style_id", "abv", "ibu", "srm", "descript"
            }, "requests");
            List<Request> requests = new List<Request> { };
            foreach (string[] requestAttributes in requestsAttributes)
            {
                Style sty = new Style("Unknown Style", -1);
                Category cat = new Category("Unknown Category", -1, new List<Style> { sty });
                foreach (Category category in categories)
                {
                    if (int.Parse(requestAttributes[3]) == category.id)
                        cat = category;
                    foreach (Style style in category.styles)
                    {
                        if (int.Parse(requestAttributes[4]) == style.id)
                            sty = style;
                    }
                }

                requests.Add(new Request(
                    int.Parse(requestAttributes[0]),
                    int.Parse(requestAttributes[1]),
                    requestAttributes[2],
                    requestAttributes[8],
                    cat,
                    sty,
                    double.Parse(requestAttributes[5]),
                    double.Parse(requestAttributes[6]),
                    double.Parse(requestAttributes[7])
                    ));
            }
            return requests;
        }

        public void AddUser(string name, string password, string gender, string birthYear)
        {
            string[] rows = new string[] { "name", "password", "gender", "birth_year" };
            string[] values = new string[] { name, password, gender, birthYear };
            Insert("users", rows, values);
            users = getUsers();
        }

        private void generateFactFile()
        {
            List<string> lines = new List<string> { };

            // first comment on top of the file
            lines.Add("%this file is automatically generated from the main program. It musn't be changed in any way, since changes will be erased by the program");

            #region creating lines from the data in the database

            /*
            the names will be automatically generated from the Id of the object in the database before being made into
            a prolog element, since the names in the database often contain unallowed characters in prolog
            */

            #region getting all beers
            List<string[]> beers = Select(new string[] { "id" }, "beers");
            foreach (string[] beer in beers)
            {
                if (int.Parse(beer[0]) != -1) // if it isn't the default beer
                {
                    string prologName = "beer" + beer[0];
                    lines.Add("beer(" + prologName + ").");
                }
            }
            #endregion
            
            #region getting all categories
            List<string[]> categories = Select(new string[] { "id" }, "categories");
            foreach (string[] category in categories)
            {
                if (int.Parse(category[0]) != -1)
                {
                    string prologName = "category" + category[0];
                    lines.Add("category(" + prologName + ").");
                }
                else
                    lines.Add("category(unknownCategory)."); // We need an unknown category, but the name "category-1" isn't acceptable
            }
            #endregion

            #region getting all styles
            List<string[]> styles = Select(new string[] { "id" }, "styles"); ;
            foreach (string[] style in styles)
            {
                if (int.Parse(style[0]) != -1)
                {
                    string prologName = "style" + style[0];
                    lines.Add("style(" + prologName + ").");
                }
                else
                    lines.Add("style(unknownStyle)."); // We need an unknown style, but the name "style-1" isn't acceptable
            }
            #endregion

            #region getting all abv (alcohol by volume)
            List<string[]> abvs = Select(new string[] { "id", "abv" }, "beers");
            foreach (string[] abv in abvs)
            {
                if (int.Parse(abv[0]) != -1) // if it isn't the default beer
                {
                    if (double.Parse(abv[1]) == 0) // we set the abv at -1 if it is unknown
                        abv[1] = "-1";
                    lines.Add("abv(beer" + abv[0] + "," + abv[1].Replace(',', '.') + ")."); // decimals should be separated with a dot, not a comma
                }
            }
            #endregion

            #region getting all ibu (internationnal bitterness unit)
            List<string[]> ibus = Select(new string[] { "id", "ibu" }, "beers");
            foreach (string[] ibu in ibus)
            {
                if (int.Parse(ibu[0]) != -1) // if it isn't the default beer
                {
                    if (double.Parse(ibu[1]) == 0) // we set the ibu at -1 if it is unknown
                        ibu[1] = "-1";
                    lines.Add("ibu(beer" + ibu[0] + "," + ibu[1].Replace(',', '.') + ")."); // decimals should be separated with a dot, not a comma
                }
            }
            #endregion

            #region getting all srm (standard reference method)
            List<string[]> srms = Select(new string[] { "id", "srm" }, "beers");
            foreach (string[] srm in srms)
            {
                if (int.Parse(srm[0]) != -1) // if it isn't the default beer
                {
                    if (double.Parse(srm[1]) == 0) // we set the srm at -1 if it is unknown
                        srm[1] = "-1";
                    lines.Add("srm(beer" + srm[0] + "," + srm[1].Replace(',', '.') + ")."); // decimals should be separated with a dot, not a comma
                }
            }
            #endregion

            #region getting all beers categories
            List<string[]> beersCat = Select(new string[] { "id", "cat_id" }, "beers");
            foreach (string[] beerCat in beersCat)
            {
                if (int.Parse(beerCat[0]) != -1) // if it isn't the default beer
                {
                    string beerPrologName = "beer" + beerCat[0];
                    string catPrologName;
                    if (int.Parse(beerCat[1]) != -1)
                        catPrologName = "category" + beerCat[1];
                    else
                        catPrologName = "unknownCategory";
                    lines.Add("beerCategory(" + beerPrologName + "," + catPrologName + ").");
                }
            }
            #endregion

            #region getting all beers styles
            List<string[]> beersSty = Select(new string[] { "id", "style_id" }, "beers");
            foreach (string[] beerSty in beersSty)
            {
                if (int.Parse(beerSty[0]) != -1) // if it isn't the default beer
                {
                    string beerPrologName = "beer" + beerSty[0];
                    string styPrologName;
                    if (int.Parse(beerSty[1]) != -1)
                        styPrologName = "style" + beerSty[1];
                    else
                        styPrologName = "unknownStyle";
                    lines.Add("beerStyle(" + beerPrologName + "," + styPrologName + ").");
                }
            }
            #endregion

            #region getting all styles categories
            List<string[]> stylesCat = Select(new string[] { "id", "cat_id" }, "styles");
            foreach (string[] styleCat in stylesCat)
            {
                string stylePrologName;
                if (int.Parse(styleCat[0]) != -1)
                    stylePrologName = "style" + styleCat[0];
                else
                    stylePrologName = "unknownStyle";

                string catPrologName;
                if (int.Parse(styleCat[1]) != -1)
                    catPrologName = "category" + styleCat[1];
                else
                    catPrologName = "unknownCategory";

                lines.Add("styleCategory(" + stylePrologName + "," + catPrologName + ").");
            }
            #endregion

            #region getting all users
            List<string[]> users = Select(new string[] { "id" }, "users");
            foreach (string[] user in users)
            {
                string prologName = "user" + user[0];
                lines.Add("user(" + prologName + ").");
            }
            #endregion

            #region getting all users birth years
            List<string[]> bys = Select(new string[] { "id", "birth_year" }, "users");
            foreach (string[] by in bys)
            {
                string prologName = "user" + by[0];
                lines.Add("birthDate(" + prologName + "," + by[1] + ").");
            }
            #endregion

            #region getting all users genders
            List<string[]> genders = Select(new string[] { "id", "gender" }, "users");
            foreach (string[] gender in genders)
            {
                string prologName = "user" + gender[0];
                string stringGender = (bool.Parse(gender[1])) ? "woman" : "man";
                lines.Add("gender(" + prologName + "," + stringGender + ").");
            }
            #endregion

            #region getting all users ratings
            List<string[]> ratings = Select(new string[] { "user_id", "beer_id", "rating" }, "ratings");
            foreach (string[] rating in ratings)
            {
                string userPlName = "user" + rating[0];
                string beerPlName = "beer" + rating[1];
                lines.Add("rates(" + userPlName + "," + beerPlName + "," + rating[2].Replace(',','.') + ").");
            }
            #endregion
            
            #endregion

            using (StreamWriter outputFile = new StreamWriter("..\\..\\..\\PrologEngine\\facts.pl"))
            {
                foreach (string line in lines)
                {
                    outputFile.WriteLine(line);
                }
            }
        }

        #region getting information from the database
        public List<string[]> Select(string[] rows, string table)
        {
            connection.Open();

            string query = "SELECT ";
            foreach (string row in rows)
                query += row + ", ";
            query = query.Substring(0, query.Length-2); // suppressing the last ", "
            query += " FROM ";
            query += table;

            List<string[]> results = new List<string[]> { };

            MySqlCommand cmd = connection.CreateCommand();
            cmd.CommandText = query;

            MySqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string[] thisRow = new string[rows.Length];
                for (int i = 0; i < rows.Length; i++)
                {
                    thisRow[i] = reader.GetString(rows[i]);
                }
                results.Add(thisRow);
            }

            connection.Close();
            return results;
        }

        public void Insert(string table, string[] rows, string[] values)
        {
            connection.Open();

            string query = "INSERT INTO " + table + " (";
            foreach (string row in rows)
                query += row + ", ";
            query = query.Substring(0, query.Length - 2); // suppressing the last ", "
            query += ") VALUES (";
            foreach (string value in values)
            {
                string v;
                try
                {
                    double test = double.Parse(value); // If it is possible to change convert value in a double
                    v = value.Replace(',', '.'); // then the comma becomes a dot ; MySql seperates decimals with a dot
                }
                catch
                {
                    v = "\"" + value +"\""; // Else, the text must be quoted
                }

                query += v + ", ";
            }
            query = query.Substring(0, query.Length - 2); // suppressing the last ", "
            query += ")";

            MySqlCommand cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.ExecuteNonQuery();

            connection.Close();
        }

        public void Update(string table, string[] rows, string[] values, int id)
        {
            string query = "UPDATE `beers` SET ";
            for (int i = 0; i < rows.Length; i++)
            {
                string v;
                try
                {
                    double test = double.Parse(values[i]); // If it is possible to change convert value in a double
                    v = values[i].Replace(',', '.'); // then the comma becomes a dot ; MySql seperates decimals with a dot
                }
                catch
                {
                    v = "\"" + values[i] + "\""; // Else, the text must be quoted
                }
                query += "`" + rows[i] + "` =" + v + ", ";
            }
            query = query.Substring(0, query.Length - 2); // suppressing the last ", "
            query += "WHERE `id` = " + id;

            connection.Open();

            MySqlCommand cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.ExecuteNonQuery();

            connection.Close();
        }
        #endregion

        public void Execute(string query)
        {
            connection.Open();
            MySqlCommand cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.ExecuteNonQuery();
            connection.Close();
        }
    }
}
