﻿#region using directives

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.PoGoUtils;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Utils;
using POGOProtos.Networking.Responses;

#endregion

namespace PoGo.NecroBot.Logic.Tasks
{
    public class RenamePokemonTask
    {
        public static async Task Execute(ISession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //await session.Inventory.RefreshCachedInventory();

            var pokemons = await session.Inventory.GetPokemons();

            foreach (var pokemon in pokemons)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var perfection = Math.Round(PokemonInfo.CalculatePokemonPerfection(pokemon));
                var pokemonName = session.Translation.GetPokemonTranslation(pokemon.PokemonId);
                // iv number + templating part + pokemonName <= 12
                var nameLength = 12 -
                                 (perfection.ToString(CultureInfo.InvariantCulture).Length +
                                  session.LogicSettings.RenameTemplate.Length - 6);
                if (pokemonName.Length > nameLength)
                {
                    pokemonName = pokemonName.Substring(0, nameLength);
                }
                var newNickname = string.Format(session.LogicSettings.RenameTemplate, pokemonName, perfection);
                var oldNickname = pokemon.Nickname.Length != 0 ? pokemon.Nickname : pokemon.PokemonId.ToString();

                // If "RenameOnlyAboveIv" = true only rename pokemon with IV over "KeepMinIvPercentage"
                // Favorites will be skipped
                if ((!session.LogicSettings.RenameOnlyAboveIv ||
                     perfection >= session.LogicSettings.KeepMinIvPercentage) &&
                    newNickname != oldNickname)
                {
                    var result = await session.Client.Inventory.NicknamePokemon(pokemon.Id, newNickname);

                    if (result.Result == NicknamePokemonResponse.Types.Result.Success)
                    {
                        pokemon.Nickname = newNickname;
                        await session.Inventory.DeletePokemonFromInvById(pokemon.Id);
                        await session.Inventory.AddPokemonToCache(pokemon);

                        session.EventDispatcher.Send(new NoticeEvent
                        {
                            Message = session.Translation.GetTranslation(
                                TranslationString.PokemonRename,
                                session.Translation.GetPokemonTranslation(pokemon.PokemonId),
                                pokemon.Id,
                                oldNickname,
                                newNickname
                            )
                        });
                    }
                    //Delay only if the pokemon was really renamed!
                    DelayingUtils.Delay(session.LogicSettings.RenamePokemonActionDelay, 500);
                }
            }
        }
    }
}