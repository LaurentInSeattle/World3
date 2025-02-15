﻿namespace Lyt.World.Model
{
    using Lyt.World.Engine;

    using System;

    public sealed partial class FluModel : Simulator
    {
        #region Dynamo Model 
        /*
         *     SIMPLE EPIDEMIC MODEL
            NOTE
            L     SUSC.K=SUSC.J+DT*(-INF.JK)
            N     SUSC=988
            NOTE  SUSPECTIBLE POPULATION (PEOPLE)
            R     INF.KL=SICK.K*CNTCTS.K*FRSICK
            NOTE  INFECTION RATE (PEOPLE PER DAY)
            C     FRSICK=0.05
            NOTE  FRACTION OF CONTACTS BECOMING SICK
            NOTE  (DIMENSIONLESS)
            L     SICK.K=SICK.J+DT*(INF.JK-CURE.JK)
            N     SICK=2
            NOTE  SICK POPULATION (PEOPLE)
            A     CNTCTS.K=TABLE(TABCON,SUSC.K/TOTAL,0,1,0.2)
            NOTE  SUSPECTIBLE CONTACTED PER INFECTED PERSON
            NOTE  PER DAY (PEOPLE PER PERSON PER DAY)
            T     TABCON=0/2.8/5.5/8/9.5/10
            NOTE  TABLE FOR CONTACTS
            N     TOTAL=SUSC+SICK+RECOV
            NOTE  TOTAL POPULATION (PEOPLE)
            R     CURE.KL=SICK.K/DUR
            NOTE  CURE RATE (PEOPLE PER DAY)
            C     DUR=10
            NOTE  DURATION OF DISEASE (DAYS)
            L     RECOV.K=RECOV.J+DT*CURE.JK
            N     RECOV=10
            NOTE  RECOVERED POPULATION (PEOPLE)
            NOTE
            SPEC  DT=0.25,LENGTH=50,PRTPER=5,PLTPER=0.5
            PRINT SUSC,SICK,RECOV,INF,CURE
            PLOT  SUSC=W,SICK=S,RECOV=R(0,1000)/INF=I,CURE=C(0,200)
            RUN   SIMPLE
            NOTE
            NOTE  **** MODIFIED MODEL WITH DELAY (INCUBATION)
            NOTE
            EDIT  SIMPLE
            NOTE  INCUBATION DELAY (DAYS)
            C     TSS=3
            NOTE  FRACTION OF CONTACTS SHOWING SYMPTOMS
            NOTE  (DIMENSIONLESS)
            R     SYMP.KL=DELAY1(INF.JK,TSS)
            L     SICK.K=SICK.J+DT*(SYMP.JK-CURE.JK)
            RUN   DELAY
         */
        #endregion Dynamo Model 

        private Auxiliary population;

        private Auxiliary recoveryPerDay;
        private Auxiliary deathPerDay;

        private Level susceptible;

        private Level infected;     // incubating
        private Level sick;         // home isolated 
        private Level recovered;
        private Level dead;

        private Rate infectedPerDay;
        private PureDelay sickPerDay;
        private PureDelay outcome;
        private PureDelay vulnerablePerDay;

        // TODO: Make these values simulation parameters 
        private readonly double contacts = 8; // people
        private readonly double rawInfectionRate = 0.035; // dimensionless 

        private readonly double incubationDays = 12; // days
        private readonly double sicknessDays = 14; // days
        private readonly double rawLethalityRate = 0.025; // dimensionless 
        private readonly double lostImmunityDays =  3 * 30; // dimensionless 

        private void CreateModel()
        {

            this.sector = "Flu Model";
            this.subSector = "";

            this.population = new Auxiliary("population", "persons")
            {
                UpdateFunction = delegate ()
                {
                    return AsInt(susceptible.K + infected.K + sick.K + recovered.K);
                }
            };

            this.susceptible = new Level("susceptible", "persons", 10_000_000.0)
            {
                CannotBeNegative = true,
                UpdateFunction = delegate ()
                {
                    return AsInt(Positive(susceptible.J - (infectedPerDay.J - vulnerablePerDay.J  ))) ;
                }
            };

            this.infected = new Level("infected", "persons", 0.0)
            {
                CannotBeNegative = true,
                UpdateFunction = delegate ()
                {
                    double newInfected = AsInt( infectedPerDay.J - sickPerDay.J) ; 
                    return AsInt(Positive(infected.J + newInfected));
                }
            };


            this.infectedPerDay = new Rate("infectedPerDay", "people per day")
            {
                CannotBeNegative = true,
                UpdateFunction = delegate ()
                {
                    double infectedContacts = this.rawInfectionRate * this.contacts * this.infected.K; 
                    double value = 0.0; 
                    if ( infectedContacts < this.susceptible.K )
                    {
                        value = infectedContacts;
                    }
                    else
                    {
                        value = this.susceptible.K; 
                    }

                    return AsInt(Positive(value));
                }
            };

            //this.contacts =
            //    new Table("contacts", "Persons per person and per day",
            //    new double[] { 0.0, 2.8, 5.5, 8, 9.5, 10 }, 0, 10, 0.5)
            //    {
            //        UpdateFunction = delegate () { return susceptible.K / this.population.K; }
            //    };


            this.sick = new Level("sick", "persons", 0.0)
            {
                CannotBeNegative = true,
                UpdateFunction = delegate ()
                {
                    double newSick = AsInt(sickPerDay.J) - recoveryPerDay.J - deathPerDay.J;
                    return AsInt(Positive(sick.J + newSick));
                }

            };

            this.sickPerDay = new PureDelay("sickPerDay", "persons per day", incubationDays, "infectedPerDay") { };

            this.outcome = new PureDelay("outcome", "persons", sicknessDays, "sickPerDay") { };

            this.recoveryPerDay = new Auxiliary("recoveryPerDay", "persons per day")
            {
                UpdateFunction = delegate ()
                {
                    return AsInt(outcome.K * (1.0 - rawLethalityRate));
                }
            };

            this.deathPerDay = new Auxiliary("deathPerDay", "persons per day")
            {
                CannotBeNegative = true,
                UpdateFunction = delegate ()
                {
                    return AsInt(outcome.K * rawLethalityRate);
                }
            };

            this.dead = new Level("dead", "persons", 0)
            {
                CannotBeNegative = true,
                UpdateFunction = delegate ()
                {
                    return AsInt(dead.J + deathPerDay.J);
                }
            };

            this.recovered = new Level("recovered", "persons", 0)
            {
                CannotBeNegative = true,
                UpdateFunction = delegate ()
                {
                    return AsInt(Positive(recovered.J + recoveryPerDay.J - vulnerablePerDay.J ));
                }
            };

            this.vulnerablePerDay = new PureDelay("vulnerablePerDay", "persons", lostImmunityDays, "recoveryPerDay") { };
        }

        private readonly string[] dependencies =
        {
            // Equation         // Depends On
            "outcome",          // sick
            "recoveryPerDay",   // outcome
            "deathPerDay",      // outcome
            "population",       // susceptible infected sick recovered 
            "contacts",         // susceptible population
            "infectedPerDay",   // infected
            "sickPerDay",       // infected per day 
            "vulnerablePerDay", // recovered per day
        };
    }
}
