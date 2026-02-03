#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassWithInit0002
{
    public static class Program
    {
        public static void Main()
        {
#line 45 "class_with_init_0002.spy"
            var book1 = new Book("1984", "Orwell", 328);
#line 46 "class_with_init_0002.spy"
            global::Sharpy.Core.Exports.Print(book1.Title);
#line 47 "class_with_init_0002.spy"
            global::Sharpy.Core.Exports.Print(book1.Pages);
#line 48 "class_with_init_0002.spy"
            global::Sharpy.Core.Exports.Print(book1.GetStatus());
#line 50 "class_with_init_0002.spy"
            book1.Checkout();
#line 51 "class_with_init_0002.spy"
            global::Sharpy.Core.Exports.Print(book1.GetStatus());
#line 53 "class_with_init_0002.spy"
            var lib = new Library("City Library");
#line 54 "class_with_init_0002.spy"
            lib.AddBook();
#line 55 "class_with_init_0002.spy"
            lib.AddBook();
#line 56 "class_with_init_0002.spy"
            global::Sharpy.Core.Exports.Print(lib.GetCount());
        }
    }

    public class Book
    {
        public string Title;
        public string Author;
        public int Pages;
        public bool IsAvailable;
        public void Checkout()
        {
#line 17 "class_with_init_0002.spy"
            this.IsAvailable = false;
        }

        public void ReturnBook()
        {
#line 20 "class_with_init_0002.spy"
            this.IsAvailable = true;
        }

        public string GetStatus()
        {
#line 23 "class_with_init_0002.spy"
            if (this.IsAvailable)
            {
#line 24 "class_with_init_0002.spy"
                return "Available";
            }
            else
            {
#line 26 "class_with_init_0002.spy"
                return "Checked Out";
            }
        }

        public Book(string title, string author, int pages)
        {
#line 11 "class_with_init_0002.spy"
            this.Title = title;
#line 12 "class_with_init_0002.spy"
            this.Author = author;
#line 13 "class_with_init_0002.spy"
            this.Pages = pages;
#line 14 "class_with_init_0002.spy"
            this.IsAvailable = true;
        }
    }

    public class Library
    {
        public string Name;
        public int TotalBooks;
        public void AddBook()
        {
#line 38 "class_with_init_0002.spy"
            this.TotalBooks = this.TotalBooks + 1;
        }

        public int GetCount()
        {
#line 41 "class_with_init_0002.spy"
            return this.TotalBooks;
        }

        public Library(string name)
        {
#line 34 "class_with_init_0002.spy"
            this.Name = name;
#line 35 "class_with_init_0002.spy"
            this.TotalBooks = 0;
        }
    }
}
