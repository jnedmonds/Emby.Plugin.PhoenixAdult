import PAsearchSites
import PAutils

supported_lang = ['en', 'de', 'fr', 'es', 'it']


def getJSONfromAPI(query, lang, siteNum):
    params = json.dumps({'country': 'us', 'keyword': query, 'user_language': lang})
    headers = {
        'Content-Type': 'application/json',
        'Accept-Language': lang,
    }
    data = PAutils.HTTPRequest(PAsearchSites.getSearchSearchURL(siteNum), headers=headers, params=params).json()

    return data['items']


def getJSONfromPage(url, lang, siteNum):
    headers = {
        'Referer': PAsearchSites.getSearchBaseURL(siteNum)
    }
    cookies = {'AGEGATEPASSED': '1'}

    if lang in supported_lang:
        url = url.replace('://www.', '://%s.' % lang, 1)
        headers['Accept-Language'] = lang

    req = PAutils.HTTPRequest(url, headers=headers, cookies=cookies)
    detailsPageElements = HTML.ElementFromString(req.text)

    if req:
        scriptData = detailsPageElements.xpath('//div[./div[@id="profile_page"]]/script')[0].text_content()
        jsonData = re.search(r'\{.*\}', scriptData)
        return json.loads(jsonData.group(0))

    return None


def search(results, lang, siteNum, searchData):
    searchResults = getJSONfromAPI(searchData.title, lang, siteNum)
    for searchResult in searchResults:
        if searchResult['contentType'] == 'video':
            titleNoFormatting = searchResult['title']
            userId = searchResult['u_id']
            userVideoId = searchResult['uv_id']
            userNickname = searchResult['nick']
            cleanTitle = slugify(titleNoFormatting, separator='-', regex_pattern=r'[^-A-z0-9]+')
            sceneURL = '%s/profil/%s-%s/videos/%s-%s' % (PAsearchSites.getSearchBaseURL(siteNum), userId, userNickname, userVideoId, cleanTitle)
            curID = PAutils.Encode(sceneURL)

            date = searchResult['onlineAt']
            if date:
                try:
                    releaseDate = datetime.strptime(date, '%d/%m/%y').strftime('%Y-%m-%d')
                except:
                    releaseDate = parse(searchResult['latestPictureChange'].split('T')[0].strip()).strftime('%Y-%m-%d')
            else:
                releaseDate = searchData.dateFormat() if searchData.date else ''

            displayDate = releaseDate if date else ''

            if searchData.date and displayDate:
                score = 100 - Util.LevenshteinDistance(searchData.date, releaseDate)
            else:
                score = 100 - Util.LevenshteinDistance(searchData.title.lower(), titleNoFormatting.lower())

            results.Append(MetadataSearchResult(id='%s|%d|%s' % (curID, siteNum, releaseDate), name='%s [%s/%s] %s' % (PAutils.parseTitle(titleNoFormatting, siteNum), PAsearchSites.getSearchSiteName(siteNum), userNickname, displayDate), score=score, lang=lang))

    return results


def update(metadata, lang, siteNum, movieGenres, movieActors, art):
    metadata_id = str(metadata.id).split('|')
    sceneURL = PAutils.Decode(metadata_id[0])

    detailsPageElements = getJSONfromPage(sceneURL, lang, siteNum)
    videoPageElements = detailsPageElements['content']
    userPageElements = detailsPageElements['profileHeader']['profileAvatar']

    # Title
    metadata.title = PAutils.parseTitle(videoPageElements['title']['text'], siteNum).strip()

    # Summary
    metadata.summary = videoPageElements['description']['text'].strip()

    # Studio
    metadata.studio = 'My Dirty Hobby'

    # Tagline and Collection(s)
    tagline = userPageElements['title'].strip()
    metadata.tagline = tagline
    metadata.collections.add(tagline)

    # Release Date
    date = videoPageElements['subtitle']['text'].strip()
    if date:
        date_object = parse(date)
        metadata.originally_available_at = date_object
        metadata.year = metadata.originally_available_at.year

    # Genres
    for genreLink in videoPageElements['categories']['items']:
        genreName = genreLink['text'].strip().lower()

        movieGenres.addGenre(genreName)

    # Actor(s)
    actorName = userPageElements['title'].strip()
    actorPhotoURL = userPageElements['thumbImg']['src'].strip()

    movieActors.addActor(actorName, actorPhotoURL)

    # Posters
    art.append(videoPageElements['videoNotPurchased']['thumbnail']['src'].strip())

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
                if width > 1 or height > width:
                    # Item is a poster
                    metadata.posters[posterUrl] = Proxy.Media(image.content, sort_order=idx)
                if width > 100 and width > height:
                    # Item is an art item
                    metadata.art[posterUrl] = Proxy.Media(image.content, sort_order=idx)
            except:
                pass

    return metadata
