# Použijeme lehkou, ale plnohodnotnou verzi Postgresu
FROM postgres:16-alpine

# Zkopírujeme náš inicializační skript do speciální složky.
# Všechny .sql skripty v této složce Postgres automaticky spustí 
# při prvním zapnutí (když je databáze prázdná).
COPY init.sql /docker-entrypoint-initdb.d/

# (Pokud byste chtěl v budoucnu měnit nějaká specifická nastavení 
# Postgresu pro vyšší výkon, dělalo by se to zde).