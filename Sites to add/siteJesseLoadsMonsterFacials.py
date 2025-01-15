import PAsearchSites
import PAutils


def search(results, lang, siteNum, searchData):
    idx = 1
    tourPageURL = '%s/tour_%02d.html' % (PAsearchSites.getSearchSearchURL(siteNum), idx)
    req = PAutils.HTTPRequest(tourPageURL)

    while req.ok:
        tourPageElements = HTML.ElementFromString(req.text)
        for sceneResult in tourPageElements.xpath('//table[@width="880"]'):
            summary = sceneResult.xpath('.//td[@height="105" or @height="90"]')[0].text_content().replace('\n', ' ').strip()
            summaryID = PAutils.Encode(re.sub(r'\s+', ' ', summary))

            actorFirstName = summary.split()[0].strip().lower()
            actorNameFromImg = sceneResult.xpath('.//img[contains(@src, "fft")]/@src')[0].split('_')[-1].split('.')[0].strip().lower()
            actorName = PAutils.parseTitle('%s %s' % (actorFirstName, actorNameFromImg.split(actorFirstName)[-1]), siteNum).strip()
            titleNoFormatting = actorName

            cleanActorName = PAutils.getDictKeyFromValues(actorsDB, actorName)
            if cleanActorName:
                titleNoFormatting = ' and '.join(cleanActorName)
            else:
                cleanActorName = [actorName]
            actorNameID = PAutils.Encode('|'.join(cleanActorName))

            Log(titleNoFormatting)

            imageURL = sceneResult.xpath('.//img[contains(@src, "tour")][@width="400"]/@src')[0]
            curID = PAutils.Encode(imageURL)

            date = sceneResult.xpath('.//preceding::b[contains(., "Update")]')
            if date:
                releaseDate = datetime.strptime(date[-1].text_content().split(':')[-1].strip(), '%m/%d/%Y').strftime('%Y-%m-%d')
            else:
                releaseDate = searchData.dateFormat() if searchData.date else ''

            displayDate = releaseDate if date else ''

            if searchData.date and displayDate:
                score = 100 - Util.LevenshteinDistance(searchData.date, displayDate)
            else:
                score = 100 - Util.LevenshteinDistance(searchData.title.lower(), titleNoFormatting.lower())

            results.Append(MetadataSearchResult(id='%s|%d|%s|%s|%s' % (curID, siteNum, releaseDate, actorNameID, summaryID), name='%s [%s] %s' % (titleNoFormatting, PAsearchSites.getSearchSiteName(siteNum), displayDate), score=score, lang=lang))

            if searchData.date and int(score - Util.LevenshteinDistance(searchData.title.lower(), titleNoFormatting.lower())) == 100:
                break
        else:
            idx += 1
            tourPageURL = '%s/tour_%02d.html' % (PAsearchSites.getSearchSearchURL(siteNum), idx)
            req = PAutils.HTTPRequest(tourPageURL)
            continue
        break

    return results


def update(metadata, lang, siteNum, movieGenres, movieActors, art):
    metadata_id = str(metadata.id).split('|')
    poster = PAutils.Decode(metadata_id[0])
    if not poster.startswith('http'):
        poster = '%s/%s' % (PAsearchSites.getSearchSearchURL(siteNum), poster)
    sceneDate = metadata_id[2]
    actors = PAutils.Decode(metadata_id[3]).split('|')
    summary = PAutils.Decode(metadata_id[4])

    # Title
    metadata.title = '%s from JesseLoadsMonsterFacials.com' % ' and '.join(actors)

    # Summary
    metadata.summary = summary

    # Studio
    metadata.studio = 'Jesse Loads Monster Facials'
    metadata.collections.add(metadata.studio)

    # Release Date
    if sceneDate:
        date_object = parse(sceneDate)
        metadata.originally_available_at = date_object
        metadata.year = metadata.originally_available_at.year

    # Genres
    movieGenres.addGenre('Facial')

    # Actor(s)
    for actor in actors:
        if actor != 'Compilation':
            actorName = actor
            actorPhotoURL = ''

            movieActors.addActor(actorName, actorPhotoURL)

    # Posters
    art.append(poster)

    Log('Artwork found: %d' % len(art))
    for idx, posterUrl in enumerate(art, 1):
        if not PAsearchSites.posterAlreadyExists(posterUrl, metadata):
            # Download image file for analysis
            try:
                image = PAutils.HTTPRequest(posterUrl)
                im = StringIO(image.content)
                resized_image = Image.open(im)
                width, height = resized_image.size
                # Add the image proxy items to the collection
                if width > 1:
                    # Item is a poster
                    metadata.posters[posterUrl] = Proxy.Media(image.content, sort_order=idx)
                if width > height:
                    # Item is an art item
                    metadata.art[posterUrl] = Proxy.Media(image.content, sort_order=idx)
            except:
                pass

    return metadata


actorsDB = {
    'Aaliyah Love': ['We Aaliyahlove'],
    'Addison O\'Riley': ['This Addisonoriley'],
    'Alana Foxx': ['Yes. Alanafoxx'],
    'Alexandria Devine': ['If Alexandriadevine'],
    'Alexis Grace': ['This Alexisgrace'],
    'Alexis James': ['Alexis Firsttimefacials'],
    'Alice March': ['Who\'s Alicemarch'],
    'Alix Lovell': ['Some Alixlovell'],
    'Andi Anderson': ['Whoever Andianderson'],
    'Angel Smalls': ['This Angelsmalls'],
    'Anna Bell Peaks': ['Annabelle Annabellpeaks'],
    'Anna Claire Clouds': ['Don\'t Annaclaireclouds'],
    'Athena Anderson': ['Athina Athenaanderson'],
    'Aubrey Babcock': ['Aubrey Aubrybabcock'],
    'Audrey Miles': ['You Audreymiles'],
    'Bentley': ['As Bentley'],
    'Breezy Bri': ['True Breezybri'],
    'Brianna Brooks': ['This Briannabrooks'],
    'Brooklyn Daniels': ['Once Brooklyndaniels'],
    'Brooklyn Jade': ['With Brooklynjade'],
    'Cameron Dee': ['One Camerondee'],
    'Candy': ['This Candy'],
    'Carli Evelyn': ['Carly Carlievelyn'],
    'Charly Summer': ['At Charlysummer'],
    'Charlyse Angel': ['This Charlyseaqngel'],
    'Chloe Couture': ['Chloe Couturepennypax'],
    'Cindy Jones': ['What Cindyjones'],
    'Compilation': ['This Firsttimefacials', 'This Facialcomps', 'We Facialcomps'],
    'Courtney Shea': ['Sometimes Courtneyshea'],
    'Danica Lamb': ['They Danicalamb'],
    'Delilah Day': ['Delila Hday'],
    'Destiny': ['Desitny Destiny'],
    'Emma Snow': ['Don\'t Emmasnow'],
    'Gianna Love': ['One Giannalove'],
    'Hayden Nite': ['At Haydennite'],
    'Heidi Hollywood': ['The Heidihollywood', 'Hello Heidihollywood'],
    'Jackie Cruz': ['Our Jackiecruz'],
    'Jade Jantzen': ['This Jadejantzen'],
    'Jamie Jackson': ['This Jamiejackson'],
    'Jenna Ivory': ['This Jennaivory'],
    'Jennifer White': ['The Jenniferwhite'],
    'Joseline Kelly': ['Josline Joselinekelly'],
    'Kara Stone': ['This Karastone'],
    'Karma Rx': ['After Karmarx'],
    'Karmen Karma': ['The Karmenkarma'],
    'Kassondra Raine': ['If Kassondraraine'],
    'Katerine': ['This Katerine'],
    'Katie Kingerie': ['Third Katiekingerie'],
    'Katie Summers': ['This Katiesummers'],
    'Katreena Lee': ['This Katreenalee'],
    'Katrina Zova': ['The Katrinakox', 'Kox'],
    'Katt Dylan': ['This Kattdylan'],
    'Kendra Cole': ['This Kendracole'],
    'Kendra Heart': ['Some Kendraheart'],
    'Kendra Secrets': ['Some Kendrasecrets'],
    'Kennedy': ['Kennedy, Kennedy'],
    'Kimberly Gates': ['You Kimberlygates'],
    'Kimmy Granger': ['Chad Kimmygranger', 'This Stassipennykimmy'],
    'Kodi Jane': ['Don\'t Kodijane'],
    'Layton Benton': ['This Laytonbenton'],
    'Lex Chevelle': ['Kealoha'],
    'Lexi Brooks': ['This Lexibrooks'],
    'Lexi Lowe': ['Lexi Lowepennypax'],
    'Lily Labeau': ['This Lilylabeaupennypax'],
    'Lizz Tayler': ['What Lizztayler'],
    'Lizzy London': ['This Lizzylondon'],
    'Loni Evans': ['The Lonievans', 'We Lonievans', 'When Lonievans'],
    'Madison Swan': ['This Madisonswan'],
    'Mae Olsen': ['Just Maeolsen'],
    'Maia Davis': ['They Maiadavis'],
    'Mandy Sweet': ['There Mandysweet'],
    'Mary Jane Mayhem': ['Mary Janemayhem', 'This Maryjanemayhem'],
    'Melody Jordan': ['This Melodyjordan'],
    'Mena Mason': ['When Menamason'],
    'Milaneils': ['When Milaneils'],
    'Molly Rae': ['This Mollyrae', 'When Mollyrae'],
    'Nadia White': ['We Nadiawhite'],
    'Natalie Queen': ['Our Nataliequeen', 'Picture Nataliequeen'],
    'Ney Amber': ['It Neyamber'],
    'Nicole Aniston': ['This Nicoleaniston'],
    'Nicole Ferrera': ['When Nicoleferrera'],
    'Payton Preslee': ['At Paytonpreslee'],
    'Penny Pax': ['Chloe Couturepennypax', 'This Lilylabeaupennypax', 'Lexi Lowepennypax', 'The Valentinapenny', 'This Stassipennykimmy'],
    'Riley Reid': ['If Rileyreid'],
    'Ryder Skye': ['They Ryderskye'],
    'Sabrina Sweet': ['Sabrina\'s Sabrinasweet'],
    'Scarlett Monroe': ['The Scarlettmonroe'],
    'Scarlett Venom': ['Scarlet Tvenom'],
    'Sea J Raw': ['Sea Jraw'],
    'Sedusa Drakaina': ['As Sedusadrakaina'],
    'Sheena Ryder': ['This Sheenaryder'],
    'Sidnee Joe': ['Sindee Sidneejoe'],
    'Sindee Shay': ['Despite Sindeeshay'],
    'Sophia Fiore': ['This Sophiafiore'],
    'Stassi Sinclair': ['This Stassipennykimmy'],
    'Summer Vixen': ['Our Summervixen'],
    'Torrie Madison': ['This Torriemadison'],
    'Tweety Valentine': ['Despite Tweetyvalentine'],
    'Valentina Nappi': ['The Valentinapenny'],
    'Victoria White': ['It E'],
    'Ziggy Star': ['Some Ziggystar'],
    'Zoe Clark': ['Cock Zoeclark'],
}
