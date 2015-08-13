About
=====

Project site: https://github.com/timabell/ef-edmx-nav-namer

Renames the navigation properties in a `.edmx` file to match the foreign key
names based on a pattern match.

Useful when you're doing databse-first and the update-from-database feature
makes a mess of the names.

Source code of https://github.com/timabell/ef-edmx-sorter used as a starting
point for creating this.

This assume senisble naming of your foreign keys (FKs) in your database.

For the following key name: FK_Parent_Child it will set the navigation
properties at each end to:

* on parent: "Child"
* on child: "Parent"

which is based entirely on the string name of the FK.

Usage
=====

    EfEdmxNavNamer.exe -i path\to\your\Model.edmx

Licence
=======

Apache 2.0 https://www.apache.org/licenses/LICENSE-2.0 as per the project this
was based on.
