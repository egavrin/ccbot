ReviewBot
=========

ReviewBot is a tool for automatically inserting and verifying [CodeContracts](http://research.microsoft.com/en-us/projects/contracts/)

Status / Contributing
=====================

ReviewBot is very much a work-in-progress.  Please go ahead a fix any bug you find (and make a pull request).

Requirements
============

[CodeContracts](https://visualstudiogallery.msdn.microsoft.com/1ec7db13-3363-46c9-851f-1ce455f66970)

Building
========

Just open the solution in Visual Studio

Usage
=====

Right now the easiest way to run ReviewBot is the AutoBot project.  All you have to change is projPath and slnPath in Main.

The solution and project you put in those paths must be in a git repo.

It will create a branch called "reviewbot" and insert the contracts into the source files in that branch.
