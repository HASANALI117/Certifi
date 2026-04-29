# .NET CLI Cheatsheet — IT8118 Training Platform

## Solution & Projects

```bash
dotnet sln add <path/to.csproj>               # add project to solution
dotnet sln remove <path/to.csproj>            # remove project from solution
dotnet sln list                               # list all projects in solution
dotnet new webapi -n ProjectName              # new Web API project
dotnet new mvc -n ProjectName                 # new MVC project
dotnet add reference ../Other/Other.csproj    # add project reference (run from project folder)
```

## Packages

```bash
dotnet add package PackageName                # install NuGet package
dotnet remove package PackageName             # remove package
dotnet restore                                # restore all packages
dotnet list package                           # list installed packages
```

## Build & Run

```bash
dotnet build                                  # build solution
dotnet run                                    # run (from project folder)
dotnet run --project TrainingPlatform.API     # run specific project (from solution root)
dotnet watch run                              # run with hot reload
```

## EF Core Migrations
> Run from the API project folder. Requires `dotnet-ef` tool and `Microsoft.EntityFrameworkCore.Design` package.

```bash
dotnet tool install -g dotnet-ef              # install EF CLI tool (once)

dotnet ef migrations add MigrationName        # create migration
dotnet ef migrations remove                   # remove last migration
dotnet ef migrations list                     # list all migrations
dotnet ef database update                     # apply migrations to database
dotnet ef database drop                       # drop database (then re-run update)
dotnet ef migrations script > database/schema.sql   # generate schema.sql for submission
```

## User Secrets
> Store JWT secrets and connection strings here — never commit to Git.

```bash
dotnet user-secrets init                      # initialize (run from project folder)
dotnet user-secrets set "Jwt:Secret" "your-secret"
dotnet user-secrets set "ConnectionStrings:Default" "your-conn-string"
dotnet user-secrets list                      # view all secrets
```

## This Project

```bash
# Run API
dotnet run --project TrainingPlatform.API

# Run MVC
dotnet run --project TrainingPlatform.MVC

# Run Reports
dotnet run --project TrainingPlatform.Reports

# Add API project back to solution
dotnet sln TrainingPlatform.slnx add TrainingPlatform.API/TrainingPlatform.API.csproj
```
