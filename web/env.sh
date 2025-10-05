#!/bin/sh
# This script iterates over all environment variables that starts with 'EXPOSE_APP_'
# then finds and replaces the key with the corresponding value in files 
# within the defined directory.
directory=/usr/share/nginx/html

env | grep -E '^EXPOSE_APP_' | while IFS= read -r i
do
    key=$(echo "$i" | cut -d '=' -f 1)
    value=$(echo "$i" | cut -d '=' -f 2-)
    echo "$key"="$value"
    # sed All files
    find $directory -type f -exec sed -i "s|${key}|${value}|g" '{}' +

    # sed JS and CSS only
    # find $directory -type f \( -name '*.js' -o -name '*.css' \) -exec sed -i "s|${key}|${value}|g" '{}' +
done